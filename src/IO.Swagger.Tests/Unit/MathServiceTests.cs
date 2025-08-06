using IO.Swagger.Models.Math;
using IO.Swagger.Services.Math;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IO.Swagger.Tests.Unit;

public class MathServiceTests {
	private readonly Mock<ILogger<MathService>> _mockLogger;
	private readonly MathService _mathService;

	public MathServiceTests() {
		_mockLogger = new Mock<ILogger<MathService>>();
		_mathService = new MathService(_mockLogger.Object);
	}

	[Theory]
	[InlineData(10.0,5.0,15.0)]
	[InlineData(0.0,0.0,0.0)]
	[InlineData(-5.0,3.0,-2.0)]
	[InlineData(100.5,25.3,125.8)]
	[InlineData(double.MaxValue,0.0,double.MaxValue)]
	public async Task PerformCalculationAsync_AddOperation_ReturnsCorrectResult(double x,
																				double y,
																				double expectedResult) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = x,
			Y = y
		};
		var operationId = "add_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.Success);
		Assert.Null(result.Error);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}

	[Theory]
	[InlineData(10.0,5.0,5.0)]
	[InlineData(0.0,0.0,0.0)]
	[InlineData(-5.0,3.0,-8.0)]
	[InlineData(100.5,25.3,75.2)]
	[InlineData(5.0,10.0,-5.0)]
	public async Task PerformCalculationAsync_SubtractOperation_ReturnsCorrectResult(double x,
																					 double y,
																					 double expectedResult) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Subtract,
			X = x,
			Y = y
		};
		var operationId = "subtract_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.Success);
		Assert.Null(result.Error);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}

	[Theory]
	[InlineData(10.0,5.0,50.0)]
	[InlineData(0.0,100.0,0.0)]
	[InlineData(-5.0,3.0,-15.0)]
	[InlineData(2.5,4.0,10.0)]
	[InlineData(-3.0,-7.0,21.0)]
	public async Task PerformCalculationAsync_MultiplyOperation_ReturnsCorrectResult(double x,
																					 double y,
																					 double expectedResult) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Multiply,
			X = x,
			Y = y
		};
		var operationId = "multiply_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.Success);
		Assert.Null(result.Error);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}

	[Theory]
	[InlineData(10.0,5.0,2.0)]
	[InlineData(15.0,3.0,5.0)]
	[InlineData(-10.0,2.0,-5.0)]
	[InlineData(10.0,-2.0,-5.0)]
	[InlineData(7.5,2.5,3.0)]
	public async Task PerformCalculationAsync_DivideOperation_ReturnsCorrectResult(double x,
																				   double y,
																				   double expectedResult) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Divide,
			X = x,
			Y = y
		};
		var operationId = "divide_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.Success);
		Assert.Null(result.Error);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}

	[Theory]
	[InlineData(10.0,0.0)]
	[InlineData(-5.0,0.0)]
	[InlineData(0.0,0.0)]
	[InlineData(double.MaxValue,0.0)]
	public async Task PerformCalculationAsync_DivideByZero_ReturnsError(double x,double y) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Divide,
			X = x,
			Y = y
		};
		var operationId = "divide_by_zero_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.Equal("Division by zero is not allowed",result.Error);
		Assert.Null(result.Result);
	}

	[Fact]
	public async Task PerformCalculationAsync_NullRequest_ThrowsException() {
		// Arrange
		var operationId = "null_request_test_001";

		// Act & Assert
		await Assert.ThrowsAsync<NullReferenceException>(() => _mathService.PerformCalculationAsync(null!,operationId));
	}

	[Theory]
	[InlineData(null,5.0)]
	[InlineData(10.0,null)]
	[InlineData(null,null)]
	public async Task PerformCalculationAsync_NullValues_ReturnsError(double? x,double? y) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = x,
			Y = y
		};
		var operationId = "null_values_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.NotNull(result.Error);
		Assert.Contains("Invalid input",result.Error);
		Assert.Null(result.Result);
	}

	[Fact]
	public async Task PerformCalculationAsync_InvalidOperation_ReturnsError() {
		// Arrange
		var request = new MathRequest {
			Operation = (MathOperationType)999, // Invalid operation
			X = 10.0,
			Y = 5.0
		};
		var operationId = "invalid_operation_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.NotNull(result.Error);
		Assert.Contains("Unsupported operation",result.Error);
		Assert.Null(result.Result);
	}

	[Theory]
	[InlineData(MathOperationType.Add,1.0,2.0)]
	[InlineData(MathOperationType.Subtract,5.0,3.0)]
	[InlineData(MathOperationType.Multiply,2.0,4.0)]
	[InlineData(MathOperationType.Divide,10.0,2.0)]
	public async Task PerformCalculationAsync_ValidOperations_LogsInformation(MathOperationType operation,
																			  double x,
																			  double y) {
		// Arrange
		var request = new MathRequest {
			Operation = operation,
			X = x,
			Y = y
		};
		var operationId = "logging_test_001";

		// Act
		await _mathService.PerformCalculationAsync(request,operationId);

		// Assert - Verify that information logging was called
		_mockLogger.Verify(logger => logger.Log(LogLevel.Information,
												It.IsAny<EventId>(),
												It.Is<It.IsAnyType>((v,t) => v.ToString()!.Contains("Starting math calculation")),
												It.IsAny<Exception>(),
												It.IsAny<Func<It.IsAnyType,Exception?,string>>()),
						   Times.Once);
	}

	[Fact]
	public async Task PerformCalculationAsync_DivideByZero_LogsWarning() {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Divide,
			X = 10.0,
			Y = 0.0
		};
		var operationId = "divide_by_zero_logging_test";

		// Act
		await _mathService.PerformCalculationAsync(request,operationId);

		// Assert - Verify that warning logging was called for division by zero
		_mockLogger.Verify(logger => logger.Log(LogLevel.Warning,
												It.IsAny<EventId>(),
												It.Is<It.IsAnyType>((v,t) => v.ToString()!.Contains("Division by zero attempted")),
												It.IsAny<Exception>(),
												It.IsAny<Func<It.IsAnyType,Exception?,string>>()),
						   Times.Once);
	}

	[Theory]
	[InlineData(double.MinValue,double.MaxValue)]
	[InlineData(double.MaxValue,1.0)]
	[InlineData(1e10,1e10)]
	[InlineData(-1e15,1e5)]
	public async Task PerformCalculationAsync_ExtremeValues_HandlesGracefully(double x,double y) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = x,
			Y = y
		};
		var operationId = "extreme_values_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		// The result should either succeed or fail gracefully, not throw unhandled exceptions
		Assert.True(result.Success == true || !string.IsNullOrEmpty(result.Error));
	}

	[Theory]
	[InlineData(0.1,0.2,0.3)]
	[InlineData(1.0 / 3.0,2.0,2.333333333333333)]
	[InlineData(Math.PI,2.0,Math.PI + 2.0)]
	public async Task PerformCalculationAsync_FloatingPointPrecision_HandlesCorrectly(double x,
																					  double y,
																					  double expectedResult) {
		// Arrange
		var request = new MathRequest {
			Operation = MathOperationType.Add,
			X = x,
			Y = y
		};
		var operationId = "precision_test_001";

		// Act
		var result = await _mathService.PerformCalculationAsync(request,operationId);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.Success);
		Assert.Null(result.Error);
		Assert.Equal(expectedResult,result.Result!.Value,precision:10);
	}

	[Fact]
	public async Task PerformCalculationAsync_ConcurrentCalls_HandlesCorrectly() {
		// Arrange
		var requests = new[] {
			new MathRequest {
				Operation = MathOperationType.Add,
				X = 1.0,
				Y = 2.0
			},
			new MathRequest {
				Operation = MathOperationType.Subtract,
				X = 10.0,
				Y = 5.0
			},
			new MathRequest {
				Operation = MathOperationType.Multiply,
				X = 3.0,
				Y = 4.0
			},
			new MathRequest {
				Operation = MathOperationType.Divide,
				X = 20.0,
				Y = 4.0
			}
		};

		// Act
		var tasks = requests.Select((request,index) => _mathService.PerformCalculationAsync(request,$"concurrent_test_{index}")).ToArray();
		var results = await Task.WhenAll(tasks);

		// Assert
		Assert.All(results,result => Assert.True(result.Success));
		Assert.Equal(3.0,results[0].Result!.Value);  // 1 + 2
		Assert.Equal(5.0,results[1].Result!.Value);  // 10 - 5
		Assert.Equal(12.0,results[2].Result!.Value); // 3 * 4
		Assert.Equal(5.0,results[3].Result!.Value);  // 20 / 4
	}
}
