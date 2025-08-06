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

public class RedisServiceIntegrationTests {
	[Theory]
	[InlineData(MathOperationType.Add,10.5,5.5)]
	[InlineData(MathOperationType.Subtract,20.0,8.0)]
	[InlineData(MathOperationType.Multiply,3.5,4.0)]
	[InlineData(MathOperationType.Divide,15.0,3.0)]
	public async Task RedisCaching_MathOperations_CachesAndRetrievesCorrectly(MathOperationType operation,
																			  double x,
																			  double y) {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await builder.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate first
		var tokenRequest = new TokenRequest {
			Username = $"redis_test_user_{operation}"
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

		// Act - First request (should calculate and cache)
		var response1 = await httpClient.PostAsync("/api/math",jsonContent);
		Assert.True(response1.IsSuccessStatusCode);
		var result1 = await response1.Content.ReadFromJsonAsync<MathResponse>();
		Assert.NotNull(result1);
		Assert.True(result1.Success);
		Assert.NotNull(result1.Result);

		// Act - Second request (should retrieve from cache)
		var response2 = await httpClient.PostAsync("/api/math",jsonContent);
		Assert.True(response2.IsSuccessStatusCode);
		var result2 = await response2.Content.ReadFromJsonAsync<MathResponse>();
		Assert.NotNull(result2);
		Assert.True(result2.Success);
		Assert.NotNull(result2.Result);

		// Assert - Both results should be identical (demonstrating cache consistency)
		Assert.Equal(result1.Result,result2.Result);
		Assert.Equal(result1.Success,result2.Success);
	}

	[Theory]
	[InlineData(MathOperationType.Add,
				1.0,
				2.0,
				3.0)]
	[InlineData(MathOperationType.Subtract,
				5.0,
				3.0,
				2.0)]
	[InlineData(MathOperationType.Multiply,
				2.0,
				4.0,
				8.0)]
	[InlineData(MathOperationType.Divide,
				10.0,
				2.0,
				5.0)]
	public async Task RedisCaching_DifferentOperations_CachesIndependently(MathOperationType operation,
																		   double x,
																		   double y,
																		   double expectedResult) {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await builder.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = $"cache_independent_test_{operation}"
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
		var result = await response.Content.ReadFromJsonAsync<MathResponse>();
		Assert.NotNull(result);
		Assert.True(result.Success);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}

	[Fact]
	public async Task RedisCaching_SameOperationDifferentValues_CachesSeparately() {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await builder.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "cache_separate_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Add).ToString());

		// Test different calculations with same operation
		var calculations = new[] {
			new {
				X = 10.0,
				Y = 5.0,
				Expected = 15.0
			},
			new {
				X = 20.0,
				Y = 3.0,
				Expected = 23.0
			},
			new {
				X = 7.0,
				Y = 8.0,
				Expected = 15.0
			}
		};
		foreach (var calc in calculations) {
			var mathRequest = new MathRequest {
				Operation = MathOperationType.Add,
				X = calc.X,
				Y = calc.Y
			};
			var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

			// Act
			var response = await httpClient.PostAsync("/api/math",jsonContent);

			// Assert
			Assert.True(response.IsSuccessStatusCode);
			var result = await response.Content.ReadFromJsonAsync<MathResponse>();
			Assert.NotNull(result);
			Assert.True(result.Success);
			Assert.Equal(calc.Expected,result.Result!.Value,precision:10);
		}
	}

	[Theory]
	[InlineData(0.0,0.0)]
	[InlineData(-5.5,2.5)]
	[InlineData(999999.99,-0.01)]
	public async Task RedisCaching_EdgeCaseValues_HandlesCorrectly(double x,double y) {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await builder.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "edge_case_test_user"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Multiply).ToString());
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Multiply,
			X = x,
			Y = y
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act - First request
		var response1 = await httpClient.PostAsync("/api/math",jsonContent);
		Assert.True(response1.IsSuccessStatusCode);
		var result1 = await response1.Content.ReadFromJsonAsync<MathResponse>();

		// Act - Second request (should use cache)
		var response2 = await httpClient.PostAsync("/api/math",jsonContent);
		Assert.True(response2.IsSuccessStatusCode);
		var result2 = await response2.Content.ReadFromJsonAsync<MathResponse>();

		// Assert
		Assert.NotNull(result1);
		Assert.NotNull(result2);
		Assert.True(result1.Success);
		Assert.True(result2.Success);
		Assert.Equal(result1.Result,result2.Result);
	}

	[Fact]
	public async Task RedisCaching_MultipleUsers_CachesIndependently() {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await builder.BuildAsync();
		await app.StartAsync();
		var users = new[] { "user1","user2","user3" };
		var httpClients = new Dictionary<string,HttpClient>();

		// Authenticate all users
		foreach (var user in users) {
			var httpClient = app.CreateHttpClient("io-swagger");
			var tokenRequest = new TokenRequest {
				Username = user
			};
			var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
			var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
			httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Add).ToString());
			httpClients[user] = httpClient;
		}

		// Same calculation for all users
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Add,
			X = 15.0,
			Y = 25.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act - All users perform the same calculation
		var results = new Dictionary<string,MathResponse>();
		foreach (var user in users) {
			var response = await httpClients[user].PostAsync("/api/math",jsonContent);
			Assert.True(response.IsSuccessStatusCode);
			var result = await response.Content.ReadFromJsonAsync<MathResponse>();
			results[user] = result!;
		}

		// Assert - All results should be identical (cache working correctly across users)
		var expectedResult = 40.0;
		foreach (var user in users) {
			Assert.NotNull(results[user]);
			Assert.True(results[user].Success);
			Assert.Equal(expectedResult,results[user].Result!.Value,precision:10);
		}
	}

	[Fact]
	public async Task RedisCaching_DivideByZero_DoesNotCacheErrors() {
		// Arrange
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IO_Swagger_AppHost>();
		builder.Services.ConfigureHttpClientDefaults(clientBuilder => {
			clientBuilder.AddStandardResilienceHandler();
		});
		await using var app = await builder.BuildAsync();
		await app.StartAsync();
		var httpClient = app.CreateHttpClient("io-swagger");

		// Authenticate
		var tokenRequest = new TokenRequest {
			Username = "divide_by_zero_cache_test"
		};
		var tokenResponse = await httpClient.PostAsJsonAsync("/api/token/generate",tokenRequest);
		var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",token!.Token);
		httpClient.DefaultRequestHeaders.Add("X-ArithmeticOp-ID",((int)MathOperationType.Divide).ToString());
		var mathRequest = new MathRequest {
			Operation = MathOperationType.Divide,
			X = 10.0,
			Y = 0.0
		};
		var jsonContent = new StringContent(JsonSerializer.Serialize(mathRequest),Encoding.UTF8,"application/json");

		// Act - Multiple requests for divide by zero
		var response1 = await httpClient.PostAsync("/api/math",jsonContent);
		var result1 = await response1.Content.ReadFromJsonAsync<MathResponse>();
		var response2 = await httpClient.PostAsync("/api/math",jsonContent);
		var result2 = await response2.Content.ReadFromJsonAsync<MathResponse>();

		// Assert - Both should return error (errors typically shouldn't be cached)
		Assert.NotNull(result1);
		Assert.NotNull(result2);
		Assert.False(result1.Success!.Value);
		Assert.False(result2.Success!.Value);
		Assert.NotNull(result1.Error);
		Assert.NotNull(result2.Error);
		Assert.Contains("division by zero",result1.Error.ToLower());
		Assert.Contains("division by zero",result2.Error.ToLower());
	}
}
