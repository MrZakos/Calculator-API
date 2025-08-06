using IO.Swagger.Models;
using IO.Swagger.Models.Kafka;
using IO.Swagger.Models.Math;
using IO.Swagger.Services.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace IO.Swagger.Services.Math {
	public interface ICalculatorBusinessLogicService {
		Task<MathResponse> ExecuteCalculationWorkflowAsync(MathRequest request, string operationId);
	}

	public class CalculatorBusinessLogicService(IMathService mathService,
										   ILogger<CalculatorBusinessLogicService> logger,
										   RedisService redisServiceIntegration,
										   IConfiguration configuration,
										   IKafkaProducerService kafkaProducer,
										   IHttpContextAccessor httpContextAccessor) : ICalculatorBusinessLogicService {

		private const int DefaultCacheSeconds = 30;

		public async Task<MathResponse> ExecuteCalculationWorkflowAsync(MathRequest? request, string? operationId) {
			var stopwatch = Stopwatch.StartNew();
			var cacheHit = false;
			var userId = GetCurrentUserId();

			logger.LogInformation("Starting calculation workflow for operation ID: {OperationId}", operationId);
			
			try {
				// Validate input
				if (request is null || operationId is null) {
					logger.LogWarning("Null request received for operation ID: {OperationId}", operationId);
					return new MathResponse {
						Success = false,
						Error = "Request cannot be null"
					};
				}

				if(Enum.TryParse<MathOperationType>(operationId, out _) == false) {
					logger.LogWarning("Invalid operation type received for operation ID: {OperationId}. Operation: {Operation}",
									  operationId, request.Operation);
					return new MathResponse {
						Success = false,
						Error = "Invalid operation type"
					};
				}

				if (!request.X.HasValue || !request.Y.HasValue) {
					logger.LogWarning("Invalid operands received for operation ID: {OperationId}. X: {X}, Y: {Y}",
									  operationId, request.X, request.Y);
					return new MathResponse {
						Success = false,
						Error = "Both X and Y operands are required"
					};
				}

				if (!request.Operation.HasValue) {
					logger.LogWarning("No operation specified for operation ID: {OperationId}", operationId);
					return new MathResponse {
						Success = false,
						Error = "Operation type is required"
					};
				}

				logger.LogDebug("Input validation passed for operation ID: {OperationId}", operationId);

				// Send Kafka event for calculation started
				await SendCalculationStartedEvent(request, operationId, userId);

				// Check cache
				var cacheKey = await redisServiceIntegration.GetMathDataAsync(request);
				if (cacheKey.HasValue) {
					logger.LogInformation("CacheHit for operation ID: {OperationId}", operationId);
					cacheHit = true;
					
					var cachedResponse = new MathResponse {
						Success = true,
						Result = cacheKey.Value
					};

					// Send completion event for cache hit
					await SendCalculationCompletedEvent(request, operationId, cachedResponse, stopwatch.ElapsedMilliseconds, cacheHit, userId);
					
					return cachedResponse;
				}

				// Delegate to MathService for calculation
				var result = await mathService.CalculateAsync(request, operationId);
				
				// Cache successful results
				if (result.Result.HasValue) {
					var cacheSeconds = configuration.GetValue("Cache:MathTTLSeconds", DefaultCacheSeconds);
					await redisServiceIntegration.SetMathDataAsync(request, result.Result.Value, TimeSpan.FromSeconds(cacheSeconds));
				}

				// Send completion event
				await SendCalculationCompletedEvent(request, operationId, result, stopwatch.ElapsedMilliseconds, cacheHit, userId);

				logger.LogInformation("Calculation workflow completed for operation ID: {OperationId}, Success: {Success}", 
					operationId, result.Success);
				
				return result;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Error in calculation workflow for operation ID: {OperationId}", operationId);
				
				var errorResponse = new MathResponse {
					Success = false,
					Error = $"Workflow error: {ex.Message}"
				};

				// Send completion event for error case
				try {
					await SendCalculationCompletedEvent(request, operationId, errorResponse, stopwatch.ElapsedMilliseconds, cacheHit, userId);
				}
				catch (Exception kafkaEx) {
					logger.LogError(kafkaEx, "Failed to send error completion event for operation ID: {OperationId}", operationId);
				}

				return errorResponse;
			}
		}

		private async Task SendCalculationStartedEvent(MathRequest request, string operationId, string? userId) {
			try {
				var startedEvent = new CalculationStartedEvent {
					OperationId = operationId,
					Operation = request.Operation.ToString()!,
					X = request.X!.Value,
					Y = request.Y!.Value,
					UserId = userId,
					Timestamp = DateTime.UtcNow
				};

				await kafkaProducer.SendCalculationStartedAsync(startedEvent);
			}
			catch (Exception ex) {
				logger.LogError(ex, "Failed to send calculation started event for operation ID: {OperationId}", operationId);
				// Don't throw - Kafka failures shouldn't break the main workflow
			}
		}

		private async Task SendCalculationCompletedEvent(MathRequest? request, string? operationId, MathResponse result, 
			long executionTimeMs, bool cacheHit, string? userId) {
			try {
				if (request == null || operationId == null) return;

				var completedEvent = new CalculationCompletedEvent {
					OperationId = operationId,
					Operation = request.Operation.ToString()!,
					X = request.X!.Value,
					Y = request.Y!.Value,
					Result = result.Result,
					Success = result.Success ?? false,
					Error = result.Error,
					ExecutionTimeMs = executionTimeMs,
					CacheHit = cacheHit,
					UserId = userId,
					Timestamp = DateTime.UtcNow
				};

				await kafkaProducer.SendCalculationCompletedAsync(completedEvent);
			}
			catch (Exception ex) {
				logger.LogError(ex, "Failed to send calculation completed event for operation ID: {OperationId}", operationId);
				// Don't throw - Kafka failures shouldn't break the main workflow
			}
		}

		private string? GetCurrentUserId() {
			try {
				var user = httpContextAccessor.HttpContext?.User;
				return user?.FindFirst(ClaimTypes.Name)?.Value ?? 
					   user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
					   user?.FindFirst("sub")?.Value;
			}
			catch (Exception ex) {
				logger.LogWarning(ex, "Failed to get current user ID");
				return null;
			}
		}
	}
}
