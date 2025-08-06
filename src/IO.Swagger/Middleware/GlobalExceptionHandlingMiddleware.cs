using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IO.Swagger.Middleware;

/// <summary>
/// Global exception handling middleware with comprehensive error logging
/// </summary>
public class GlobalExceptionHandlingMiddleware(RequestDelegate next,
											   ILogger<GlobalExceptionHandlingMiddleware> logger,
											   IHostEnvironment environment) {

	public async Task InvokeAsync(HttpContext context) {
		try {
			await next(context);
		}
		catch (Exception ex) {
			await HandleExceptionAsync(context,ex);
		}
	}

	private async Task HandleExceptionAsync(HttpContext context,Exception exception) {
		var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault();
		using (LogContext.PushProperty("CorrelationId",correlationId))
			using (LogContext.PushProperty("ExceptionType",exception.GetType().Name))
				using (LogContext.PushProperty("RequestPath",context.Request.Path))
					using (LogContext.PushProperty("RequestMethod",context.Request.Method)) {
						// Log the exception with full context
						logger.LogError(exception,
										"Unhandled exception occurred for {RequestMethod} {RequestPath} with correlation ID {CorrelationId}. " + "Exception: {ExceptionMessage}, StackTrace: {StackTrace}",
										context.Request.Method,
										context.Request.Path,
										correlationId,
										exception.Message,
										exception.StackTrace);

						// Determine response based on exception type
						var response = exception switch {
										   ArgumentException => CreateErrorResponse(HttpStatusCode.BadRequest,"Invalid request parameters",correlationId),
										   UnauthorizedAccessException => CreateErrorResponse(HttpStatusCode.Unauthorized,"Access denied",correlationId),
										   KeyNotFoundException => CreateErrorResponse(HttpStatusCode.NotFound,"Resource not found",correlationId),
										   TimeoutException => CreateErrorResponse(HttpStatusCode.RequestTimeout,"Request timed out",correlationId),
										   InvalidOperationException => CreateErrorResponse(HttpStatusCode.BadRequest,"Invalid operation",correlationId),
										   _ => CreateErrorResponse(HttpStatusCode.InternalServerError,"An error occurred while processing your request",correlationId)
									   };

						// In development, include more detailed error information
						if (environment.IsDevelopment()) {
							response.Detail = exception.Message;
							response.Extensions.Add("stackTrace",exception.StackTrace);
							response.Extensions.Add("innerException",exception.InnerException?.Message);
						}
						context.Response.StatusCode = (int)response.Status!;
						context.Response.ContentType = "application/json";
						var json = JsonSerializer.Serialize(response,
															new JsonSerializerOptions {
																PropertyNamingPolicy = JsonNamingPolicy.CamelCase
															});
						await context.Response.WriteAsync(json);
					}
	}

	private static ProblemDetails CreateErrorResponse(HttpStatusCode statusCode,
													  string title,
													  string? correlationId) {
		return new ProblemDetails {
			Status = (int)statusCode,
			Title = title,
			Type = $"https://httpstatuses.com/{(int)statusCode}",
			Extensions = {
				{ "correlationId",correlationId },
				{ "timestamp",DateTimeOffset.UtcNow.ToString("O") }
			}
		};
	}
}
