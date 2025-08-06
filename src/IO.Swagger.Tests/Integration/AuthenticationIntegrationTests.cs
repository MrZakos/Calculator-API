using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.Testing;
using IO.Swagger.Models;
using IO.Swagger.Models.Math;
using IO.Swagger.Models.Token;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IO.Swagger.Tests.Integration;

public class AuthenticationIntegrationTests {
	[Theory]
	[InlineData("testuser")]
	public async Task AuthApi_ValidTokenRequest_ReturnsValidToken(string username) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");
		var tokenRequest = new TokenRequest {
			Username = username
		};

		// Act
		var response = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);

		// Assert
		Assert.True(response.IsSuccessStatusCode);
		var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
		Assert.NotNull(tokenResponse);
		Assert.NotNull(tokenResponse.Token);
		Assert.Equal("Bearer",tokenResponse.TokenType);
		Assert.Equal(username,tokenResponse.Username);
		Assert.True(tokenResponse.ExpiresAt > DateTime.UtcNow);
	}

	[Fact]
	public async Task AuthApi_EmptyRequest_ReturnsBadRequest() {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");
		var jsonContent = new StringContent("{}",Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/token/generate",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.BadRequest,response.StatusCode);
	}

	[Theory]
	[InlineData("testuser")]
	public async Task ProtectedEndpoint_ValidToken_AllowsAccess(string username) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Get a valid token first
		var tokenRequest = new TokenRequest {
			Username = username
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

		// Set authorization header
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token.Token);
		var mathRequest = new IO.Swagger.Models.Math.MathRequest {
			Operation = IO.Swagger.Models.Math.MathOperationType.Add,
			X = 10.0,
			Y = 5.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.True(response.IsSuccessStatusCode);
	}

	[Theory]
	[InlineData("")]
	[InlineData("invalid.token.here")]
	[InlineData("Bearer invalid_token")]
	[InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid")]
	public async Task ProtectedEndpoint_InvalidToken_ReturnsUnauthorized(string invalidToken) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Set invalid authorization header
		if (!string.IsNullOrEmpty(invalidToken)) {
			if (invalidToken.StartsWith("Bearer ")) {
				httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(invalidToken);
			}
			else {
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",invalidToken);
			}
		}
		var mathRequest = new IO.Swagger.Models.Math.MathRequest {
			Operation = IO.Swagger.Models.Math.MathOperationType.Add,
			X = 10.0,
			Y = 5.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.Unauthorized,response.StatusCode);
	}

	[Fact]
	public async Task ProtectedEndpoint_NoToken_ReturnsUnauthorized() {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");
		var mathRequest = new IO.Swagger.Models.Math.MathRequest {
			Operation = IO.Swagger.Models.Math.MathOperationType.Add,
			X = 10.0,
			Y = 5.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.Unauthorized,response.StatusCode);
	}

	[Theory]
	[InlineData("testuser",
				IO.Swagger.Models.Math.MathOperationType.Divide,
				10.0,
				0.0)]
	[InlineData("admin",
				IO.Swagger.Models.Math.MathOperationType.Divide,
				-5.0,
				0.0)]
	[InlineData("api_user",
				IO.Swagger.Models.Math.MathOperationType.Divide,
				0.0,
				0.0)]
	public async Task AuthenticatedDivideByZero_ValidToken_ReturnsErrorResponse(string username,
																				IO.Swagger.Models.Math.MathOperationType operation,
																				double x,
																				double y) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Get a valid token first
		var tokenRequest = new TokenRequest {
			Username = username
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

		// Set authorization header
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token.Token);
		var mathRequest = new IO.Swagger.Models.Math.MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest);
		var responseContent = await response.Content.ReadAsStringAsync();
		var mathResponse = JsonSerializer.Deserialize<MathResponse>(responseContent,
																	new JsonSerializerOptions {
																		PropertyNameCaseInsensitive = true
																	});
		Assert.NotNull(mathResponse);
		Assert.False(mathResponse.Success);
		Assert.NotNull(mathResponse.Error);
		Assert.Contains("division by zero",mathResponse.Error.ToLower());
	}
}
