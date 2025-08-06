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

public class MathApiIntegrationTests {
	
	[Theory]
	[InlineData(10.0,0.0)]            // Basic divide by zero
	[InlineData(-5.5,0.0)]            // Negative number divided by zero
	[InlineData(0.0,0.0)]             // Zero divided by zero
	[InlineData(double.MaxValue,0.0)] // Large number divided by zero
	public async Task MathApi_DivideByZero_ReturnsErrorResponse(double x,double y) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "divide_by_zero_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Divide).ToString());
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Divide,
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

	[Theory]
	[InlineData(10.0,5.0,2.0)]   // Basic division
	[InlineData(15.0,3.0,5.0)]   // Another division
	[InlineData(-10.0,2.0,-5.0)] // Negative dividend
	[InlineData(10.0,-2.0,-5.0)] // Negative divisor
	public async Task MathApi_ValidDivision_ReturnsSuccessResponse(double x,
																   double y,
																   double expectedResult) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "valid_division_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Divide).ToString());
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Divide,
			X = x,
			Y = y
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.True(response.IsSuccessStatusCode);
		var responseContent = await response.Content.ReadAsStringAsync();
		var mathResponse = JsonSerializer.Deserialize<MathResponse>(responseContent,
																	new JsonSerializerOptions {
																		PropertyNameCaseInsensitive = true
																	});
		Assert.NotNull(mathResponse);
		Assert.True(mathResponse.Success);
		Assert.Null(mathResponse.Error);
		Assert.NotNull(mathResponse.Result);
		Assert.Equal(expectedResult,mathResponse.Result.Value,precision:10);
	}

	[Theory]
	[InlineData(MathOperationType.Add,10.0,5.0,15.0)]
	[InlineData(MathOperationType.Subtract,10.0,5.0,5.0)]
	[InlineData(MathOperationType.Multiply,10.0,5.0,50.0)]
	[InlineData(MathOperationType.Divide,10.0,5.0,2.0)]
	public async Task MathApi_AllOperations_ReturnExpectedResults(MathOperationType operation,
																  double x,
																  double y,
																  double expectedResult) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = $"all_operations_test_{operation}"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)operation).ToString());
		var mathRequest = new MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.True(response.IsSuccessStatusCode);
		var responseContent = await response.Content.ReadAsStringAsync();
		var mathResponse = JsonSerializer.Deserialize<MathResponse>(responseContent,
																	new JsonSerializerOptions {
																		PropertyNameCaseInsensitive = true
																	});
		Assert.NotNull(mathResponse);
		Assert.True(mathResponse.Success);
		Assert.Null(mathResponse.Error);
		Assert.NotNull(mathResponse.Result);
		Assert.Equal(expectedResult,mathResponse.Result.Value,precision:10);
	}

	[Theory]
	[InlineData(null,5.0)]
	[InlineData(10.0,null)]
	[InlineData(null,null)]
	public async Task MathApi_NullValues_ReturnsBadRequest(double? x,double? y) {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "null_values_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Add).ToString());
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Add,
			X = x,
			Y = y
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.BadRequest,response.StatusCode);
	}

	[Fact]
	public async Task MathApi_InvalidOperation_ReturnsBadRequest() {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "invalid_operation_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID","1"); // Default to AddEnum for invalid operation test
		var mathRequest = new MathRequest {
			Operation = null,
			X = 10.0,
			Y = 5.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.BadRequest,response.StatusCode);
	}

	[Fact]
	public async Task MathApi_EmptyRequest_ReturnsBadRequest() {
		// Arrange
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "empty_request_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID","1"); // Default to AddEnum
		var jsonContent = new StringContent("{}",Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.BadRequest,response.StatusCode);
	}
}
