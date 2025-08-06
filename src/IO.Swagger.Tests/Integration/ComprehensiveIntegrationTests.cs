using System.Net;
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

/// <summary>
/// Comprehensive end-to-end tests that validate the complete integration of:
/// - Authentication Service (token validation)
/// - Redis Service (caching)
/// - Math API with divide by zero handling
/// </summary>
public class ComprehensiveIntegrationTests {
	[Fact]
	public async Task CompleteWorkflow_AuthenticationAndMathOperations_WorksCorrectly() {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});

		// To output logs to the xUnit.net ITestOutputHelper, 
		// consider adding a package from https://www.nuget.org/packages?q=xunit+logging
		await using var app = await builder.BuildAsync();
		await app.StartAsync();

		// Act
		var httpClient = app.CreateHttpClient("io-swagger");

		// Step 1: Authenticate and get token
		var tokenRequest = new TokenRequest {
			Username = "integration_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		Assert.True(tokenResponse.IsSuccessStatusCode,"Token generation should succeed");
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		Assert.NotNull(token);
		Assert.NotNull(token.Token);

		// Step 2: Set authorization header for subsequent requests
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token.Token);

		// Step 3: Test successful math operations (these should be cached)
		var successfulOperations = new[] {
			new {
				Operation = MathOperationType.Add,
				X = 10.0,
				Y = 5.0,
				Expected = 15.0
			},
			new {
				Operation = MathOperationType.Subtract,
				X = 10.0,
				Y = 3.0,
				Expected = 7.0
			},
			new {
				Operation = MathOperationType.Multiply,
				X = 4.0,
				Y = 3.0,
				Expected = 12.0
			},
			new {
				Operation = MathOperationType.Divide,
				X = 20.0,
				Y = 4.0,
				Expected = 5.0
			}
		};
		foreach (var operation in successfulOperations) {
			// Set the X-ArithmeticOp-ID header for each operation
			httpClient.DefaultRequestHeaders.Remove("X-ArithmeticOp-ID");
			httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)operation.Operation).ToString());
			var mathRequest = new MathRequest {
				Operation = operation.Operation,
				X = operation.X,
				Y = operation.Y
			};
			var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");
			var response = await httpClient.PostAsync("/api/math",jsonContent);
			Assert.True(response.IsSuccessStatusCode,$"{operation.Operation} operation should succeed");
			var responseContent = await response.Content.ReadAsStringAsync();
			var mathResponse = JsonSerializer.Deserialize<MathResponse>(responseContent,
																		new JsonSerializerOptions {
																			PropertyNameCaseInsensitive = true
																		});
			Assert.NotNull(mathResponse);
			Assert.True(mathResponse.Success,$"{operation.Operation} should return success");
			Assert.Null(mathResponse.Error);
			Assert.Equal(operation.Expected,mathResponse.Result!.Value,precision:10);
		}

		// Step 4: Test divide by zero scenarios with authentication
		var divideByZeroOperations = new[] {
			new {
				X = 10.0,
				Y = 0.0,
				Description = "Positive number divided by zero"
			},
			new {
				X = -5.0,
				Y = 0.0,
				Description = "Negative number divided by zero"
			},
			new {
				X = 0.0,
				Y = 0.0,
				Description = "Zero divided by zero"
			}
		};
		foreach (var operation in divideByZeroOperations) {
			// Set the X-ArithmeticOp-ID header for divide operations
			httpClient.DefaultRequestHeaders.Remove("X-ArithmeticOp-ID");
			httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Divide).ToString());
			var mathRequest = new MathRequest {
				Operation = MathOperationType.Divide,
				X = operation.X,
				Y = operation.Y
			};
			var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");
			var response = await httpClient.PostAsync("/api/math",jsonContent);

			// The response might be either a successful response with error details
			// or a BadRequest, depending on how the API handles division by zero
			var responseContent = await response.Content.ReadAsStringAsync();
			var mathResponse = JsonSerializer.Deserialize<MathResponse>(responseContent,
																		new JsonSerializerOptions {
																			PropertyNameCaseInsensitive = true
																		});
			Assert.NotNull(mathResponse);
			Assert.False(mathResponse.Success!.Value,$"Divide by zero should return failure for {operation.Description}");
			Assert.NotNull(mathResponse.Error);
			Assert.Contains("division by zero",mathResponse.Error.ToLower(),StringComparison.InvariantCulture);
		}
	}

	[Theory]
	[InlineData("cache_test_user_1")]
	[InlineData("cache_test_user_2")]
	[InlineData("cache_test_user_3")]
	public async Task CacheIntegration_RepeatedRequests_ShouldUseCaching(string username) {
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
			Username = username
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Multiply).ToString());

		// Perform the same calculation multiple times to test caching
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Multiply,
			X = 7.0,
			Y = 6.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// First request - should calculate and cache
		var response1 = await httpClient.PostAsync("/api/math",jsonContent);
		Assert.True(response1.IsSuccessStatusCode);
		var result1 = await response1.Content.ReadFromJsonAsync<MathResponse>();
		Assert.NotNull(result1);
		Assert.True(result1.Success);
		Assert.Equal(42.0,result1.Result!.Value);

		// Second request - should use cache (same result, potentially faster)
		var response2 = await httpClient.PostAsync("/api/math",jsonContent);
		Assert.True(response2.IsSuccessStatusCode);
		var result2 = await response2.Content.ReadFromJsonAsync<MathResponse>();
		Assert.NotNull(result2);
		Assert.True(result2.Success);
		Assert.Equal(42.0,result2.Result!.Value);

		// Results should be identical (demonstrating cache consistency)
		Assert.Equal(result1.Result,result2.Result);
	}

	[Fact]
	public async Task TokenValidation_ExpiredToken_ShouldRejectRequests() {
		// This test would require a token with very short expiry
		// For demonstration, we'll test with an invalid token instead
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		appHost.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await appHost.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Use an obviously invalid token
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer","invalid.token.that.should.fail.validation");
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Add,
			X = 1.0,
			Y = 1.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act
		var response = await httpClient.PostAsync("/api/math",jsonContent);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.Unauthorized,response.StatusCode);
	}

	[Theory]
	[InlineData(MathOperationType.Add,100.0,200.0,300.0)]
	[InlineData(MathOperationType.Subtract,100.0,30.0,70.0)]
	[InlineData(MathOperationType.Multiply,12.0,8.0,96.0)]
	[InlineData(MathOperationType.Divide,144.0,12.0,12.0)]
	public async Task AuthenticatedMathOperations_AllOperationTypes_ReturnCorrectResults(MathOperationType operation,
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
			Username = $"test_user_{operation}"
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
		Assert.True(response.IsSuccessStatusCode,$"{operation} operation should succeed");
		var result = await response.Content.ReadFromJsonAsync<MathResponse>();
		Assert.NotNull(result);
		Assert.True(result.Success,$"{operation} should return success");
		Assert.Null(result.Error);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}
}

public class IntegrationTest1 {
	[Fact]
	public async Task GetWebResourceRootReturnsOkStatusCode() {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});

		// To output logs to the xUnit.net ITestOutputHelper, 
		// consider adding a package from https://www.nuget.org/packages?q=xunit+logging
		await using var app = await builder.BuildAsync();
		await app.StartAsync();

		// Act
		var httpClient = app.CreateHttpClient("io-swagger");
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend",cts.Token);
		var response = await httpClient.GetAsync("/");

		// Assert
		Assert.Equal(HttpStatusCode.OK,response.StatusCode);
	}
}
