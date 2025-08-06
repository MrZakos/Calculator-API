using IO.Swagger.Models.Math;
using IO.Swagger.Services;
using StackExchange.Redis;
using Xunit;

namespace IO.Swagger.Tests.Unit;

public class RedisServiceUnitTests : IDisposable {
	private readonly IConnectionMultiplexer _connectionMultiplexer;
	private readonly RedisService _redisService;
	private readonly IDatabase _database;

	public RedisServiceUnitTests() {
		// Connect to local Redis instance
		_connectionMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
		_redisService = new RedisService(_connectionMultiplexer);
		_database = _connectionMultiplexer.GetDatabase();
	}

	[Theory]
	[InlineData(MathOperationType.Add,10.5,5.5,16.0)]
	[InlineData(MathOperationType.Subtract,20.0,8.0,12.0)]
	[InlineData(MathOperationType.Multiply,3.5,4.0,14.0)]
	[InlineData(MathOperationType.Divide,15.0,3.0,5.0)]
	public async Task SetMathDataAsync_ValidData_StoresCorrectlyInRedis(MathOperationType operation,
																		double x,
																		double y,
																		double result) {
		// Arrange
		var request = new MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var ttl = TimeSpan.FromSeconds(30);
		var expectedKey = $"{operation}:{x}:{y}";

		// Clean up any existing data
		await _database.KeyDeleteAsync(expectedKey);

		// Act
		await _redisService.SetMathDataAsync(request,result,ttl);

		// Assert
		var storedValue = await _database.StringGetAsync(expectedKey);
		Assert.True(storedValue.HasValue);
		Assert.True(double.TryParse(storedValue,out var parsedValue));
		Assert.Equal(result,parsedValue,precision:10);

		// Verify TTL is set
		var expiry = await _database.KeyTimeToLiveAsync(expectedKey);
		Assert.True(expiry.HasValue);
		Assert.True(expiry.Value.TotalSeconds > 29); // Should be close to 30 seconds

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Theory]
	[InlineData(MathOperationType.Add,10.5,5.5,16.0)]
	[InlineData(MathOperationType.Subtract,20.0,8.0,12.0)]
	[InlineData(MathOperationType.Multiply,3.5,4.0,14.0)]
	[InlineData(MathOperationType.Divide,15.0,3.0,5.0)]
	public async Task GetMathDataAsync_ExistingKey_ReturnsExpectedValue(MathOperationType operation,
																		double x,
																		double y,
																		double expectedResult) {
		// Arrange
		var request = new MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var expectedKey = $"{operation}:{x}:{y}";

		// Pre-populate Redis with test data
		await _database.StringSetAsync(expectedKey,expectedResult.ToString());

		// Act
		var result = await _redisService.GetMathDataAsync(request);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(expectedResult,result.Value,precision:10);

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Theory]
	[InlineData(MathOperationType.Add,1.0,2.0)]
	[InlineData(MathOperationType.Subtract,5.0,3.0)]
	[InlineData(MathOperationType.Multiply,2.0,4.0)]
	[InlineData(MathOperationType.Divide,10.0,2.0)]
	public async Task GetMathDataAsync_NonExistingKey_ReturnsNull(MathOperationType operation,
																  double x,
																  double y) {
		// Arrange
		var request = new MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var expectedKey = $"{operation}:{x}:{y}";

		// Ensure key doesn't exist
		await _database.KeyDeleteAsync(expectedKey);

		// Act
		var result = await _redisService.GetMathDataAsync(request);

		// Assert
		Assert.Null(result);
	}

	[Theory]
	[InlineData(MathOperationType.Add,1.0,2.0,true)]
	[InlineData(MathOperationType.Subtract,5.0,3.0,false)]
	[InlineData(MathOperationType.Multiply,2.0,4.0,true)]
	[InlineData(MathOperationType.Divide,10.0,2.0,false)]
	public async Task IsMathKeyExistsAsync_VariousKeys_ReturnsExpectedResult(MathOperationType operation,
																			 double x,
																			 double y,
																			 bool shouldExist) {
		// Arrange
		var expectedKey = $"{operation}:{x}:{y}";
		if (shouldExist) {
			// Create the key
			await _database.StringSetAsync(expectedKey,"42");
		}
		else {
			// Ensure key doesn't exist
			await _database.KeyDeleteAsync(expectedKey);
		}

		// Act
		var result = await _redisService.IsMathKeyExistsAsync(expectedKey);

		// Assert
		Assert.Equal(shouldExist,result);

		// Clean up
		if (shouldExist) {
			await _database.KeyDeleteAsync(expectedKey);
		}
	}

	[Theory]
	[InlineData("invalid_value")]
	[InlineData("not_a_number")]
	[InlineData("")]
	[InlineData("abc123")]
	public async Task GetMathDataAsync_InvalidRedisValue_ReturnsNull(string invalidValue) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = 1.0,
			Y = 2.0
		};
		var expectedKey = $"{MathOperationType.Add}:1:2";

		// Store invalid value in Redis
		await _database.StringSetAsync(expectedKey,invalidValue);

		// Act
		var result = await _redisService.GetMathDataAsync(request);

		// Assert
		Assert.Null(result);

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Theory]
	[InlineData(MathOperationType.Add,0.0,0.0)]
	[InlineData(MathOperationType.Divide,-5.5,2.5)]
	[InlineData(MathOperationType.Multiply,999999.99,-0.01)]
	[InlineData(MathOperationType.Subtract,double.MaxValue,double.MinValue)]
	public async Task SetAndGetMathDataAsync_EdgeCaseValues_HandlesCorrectly(MathOperationType operation,
																			 double x,
																			 double y) {
		// Arrange
		var request = new MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var expectedResult = 42.123456789; // Test value with high precision
		var ttl = TimeSpan.FromMinutes(5);
		var expectedKey = $"{operation}:{x}:{y}";

		// Clean up any existing data
		await _database.KeyDeleteAsync(expectedKey);

		// Act - Set data
		await _redisService.SetMathDataAsync(request,expectedResult,ttl);

		// Act - Get data
		var result = await _redisService.GetMathDataAsync(request);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(expectedResult,result.Value,precision:10);

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Theory]
	[InlineData(1)]     // 1 second
	[InlineData(60)]    // 1 minute  
	[InlineData(3600)]  // 1 hour
	[InlineData(86400)] // 1 day
	public async Task SetMathDataAsync_VariousTTL_SetsCorrectExpiration(int seconds) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = 1.0,
			Y = 2.0
		};
		var result = 3.0;
		var ttl = TimeSpan.FromSeconds(seconds);
		var expectedKey = $"{MathOperationType.Add}:1:2";

		// Clean up any existing data
		await _database.KeyDeleteAsync(expectedKey);

		// Act
		await _redisService.SetMathDataAsync(request,result,ttl);

		// Assert
		var actualTtl = await _database.KeyTimeToLiveAsync(expectedKey);
		Assert.True(actualTtl.HasValue);

		// Allow for small variance due to execution time
		var expectedSeconds = seconds;
		var actualSeconds = (int)actualTtl.Value.TotalSeconds;
		Assert.True(Math.Abs(expectedSeconds - actualSeconds) <= 2,$"Expected TTL around {expectedSeconds} seconds, but got {actualSeconds} seconds");

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Fact]
	public async Task SetMathDataAsync_OverwriteExistingKey_UpdatesValue() {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = 5.0,
			Y = 3.0
		};
		var initialResult = 8.0;
		var updatedResult = 15.0;
		var ttl = TimeSpan.FromMinutes(10);
		var expectedKey = $"{MathOperationType.Add}:5:3";

		// Clean up any existing data
		await _database.KeyDeleteAsync(expectedKey);

		// Act - Set initial value
		await _redisService.SetMathDataAsync(request,initialResult,ttl);
		var firstResult = await _redisService.GetMathDataAsync(request);

		// Act - Overwrite with new value
		await _redisService.SetMathDataAsync(request,updatedResult,ttl);
		var secondResult = await _redisService.GetMathDataAsync(request);

		// Assert
		Assert.NotNull(firstResult);
		Assert.Equal(initialResult,firstResult.Value);
		Assert.NotNull(secondResult);
		Assert.Equal(updatedResult,secondResult.Value);
		Assert.NotEqual(firstResult.Value,secondResult.Value);

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Fact]
	public async Task GetMathDataAsync_KeyWithoutTTL_ReturnsValue() {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Multiply,
			X = 6.0,
			Y = 7.0
		};
		var expectedResult = 42.0;
		var expectedKey = $"{MathOperationType.Multiply}:6:7";

		// Store value without TTL (persistent key)
		await _database.StringSetAsync(expectedKey,expectedResult.ToString());

		// Act
		var result = await _redisService.GetMathDataAsync(request);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(expectedResult,result.Value);

		// Verify no TTL is set
		var ttl = await _database.KeyTimeToLiveAsync(expectedKey);
		Assert.False(ttl.HasValue); // Persistent key should have no TTL

		// Clean up
		await _database.KeyDeleteAsync(expectedKey);
	}

	[Fact]
	public async Task RedisKeyFormat_DifferentOperations_GenerateUniqueKeys() {
		// Arrange
		var operations = new[] { (AddEnum:MathOperationType.Add,1.0,2.0,3.0),(SubtractEnum:MathOperationType.Subtract,1.0,2.0,-1.0),(MultiplyEnum:MathOperationType.Multiply,1.0,2.0,2.0),(DivideEnum:MathOperationType.Divide,1.0,2.0,0.5) };
		var keys = new List<string>();
		try {
			// Act - Store all operations
			foreach (var (operation,x,y,result) in operations) {
				var request = new MathRequest {
					Operation = operation,
					X = x,
					Y = y
				};
				var key = $"{operation}:{x}:{y}";
				keys.Add(key);
				await _redisService.SetMathDataAsync(request,result,TimeSpan.FromMinutes(1));
			}

			// Assert - All keys should be unique and contain correct values
			Assert.Equal(4,keys.Distinct().Count()); // All keys should be unique
			foreach (var (operation,x,y,expectedResult) in operations) {
				var request = new MathRequest {
					Operation = operation,
					X = x,
					Y = y
				};
				var actualResult = await _redisService.GetMathDataAsync(request);
				Assert.NotNull(actualResult);
				Assert.Equal(expectedResult,actualResult.Value,precision:10);
			}
		}
		finally {
			// Clean up all keys
			foreach (var key in keys) {
				await _database.KeyDeleteAsync(key);
			}
		}
	}

	public void Dispose() {
		_connectionMultiplexer?.Dispose();
	}
}
