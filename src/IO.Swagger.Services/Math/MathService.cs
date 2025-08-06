using IO.Swagger.Models;
using IO.Swagger.Models.Math;
using Microsoft.Extensions.Logging;

namespace IO.Swagger.Services.Math {
	public interface IMathService {
		Task<MathResponse> PerformCalculationAsync(MathRequest request,string operationId);
	}

	public class MathService(ILogger<MathService> logger) : IMathService {

		public Task<MathResponse> PerformCalculationAsync(MathRequest request,string operationId) {
			logger.LogInformation("Starting math calculation for operation ID: {OperationId}, Operation: {Operation}, X: {X}, Y: {Y}",
								  operationId,
								  request.Operation,
								  request.X,
								  request.Y);
			try {
				double result = 0;

				// Perform the mathematical operation
				switch (request.Operation) {
					case MathOperationType.Add:
						result = request.X.Value + request.Y.Value;
						logger.LogDebug("Addition operation: {X} + {Y} = {Result}",
										request.X,
										request.Y,
										result);
						break;
					case MathOperationType.Subtract:
						result = request.X.Value - request.Y.Value;
						logger.LogDebug("Subtraction operation: {X} - {Y} = {Result}",
										request.X,
										request.Y,
										result);
						break;
					case MathOperationType.Multiply:
						result = request.X.Value * request.Y.Value;
						logger.LogDebug("Multiplication operation: {X} * {Y} = {Result}",
										request.X,
										request.Y,
										result);
						break;
					case MathOperationType.Divide:
						if (request.Y.Value == 0) {
							logger.LogWarning("Division by zero attempted for operation ID: {OperationId}",operationId);
							return Task.FromResult(new MathResponse {
								Success = false,
								Error = "Division by zero is not allowed"
							});
						}
						result = request.X.Value / request.Y.Value;
						logger.LogDebug("Division operation: {X} / {Y} = {Result}",
										request.X,
										request.Y,
										result);
						break;
					default:
						logger.LogWarning("Invalid operation type: {Operation} for operation ID: {OperationId}",request.Operation,operationId);
						return Task.FromResult(new MathResponse {
							Success = false,
							Error = "Invalid operation"
						});
				}
				var response = new MathResponse {
					Success = true,
					Result = result
				};
				logger.LogInformation("Math calculation completed successfully for operation ID: {OperationId}, Result: {Result}",operationId,result);
				return Task.FromResult(response);
			}
			catch (Exception ex) {
				logger.LogError(ex,"Error performing calculation for operation ID: {OperationId}",operationId);
				return Task.FromResult(new MathResponse {
					Success = false,
					Error = $"Error performing calculation: {ex.Message}"
				});
			}
		}
	}
}
