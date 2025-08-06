using System;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IO.Swagger.Middleware;

/// <summary>
/// Middleware for comprehensive request/response logging with correlation tracking
/// </summary>
public class RequestResponseLoggingMiddleware(RequestDelegate next,ILogger<RequestResponseLoggingMiddleware> logger) {

	public async Task InvokeAsync(HttpContext context) {
		var stopwatch = Stopwatch.StartNew();
		var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();

		// Add correlation ID to response headers
		context.Response.Headers.Add("X-Correlation-ID",correlationId);

		// Enrich logs with correlation ID and request context
		using (LogContext.PushProperty("CorrelationId",correlationId))
			using (LogContext.PushProperty("RequestPath",context.Request.Path))
				using (LogContext.PushProperty("RequestMethod",context.Request.Method))
					using (LogContext.PushProperty("UserAgent",context.Request.Headers["User-Agent"].ToString()))
						using (LogContext.PushProperty("RemoteIpAddress",context.Connection.RemoteIpAddress?.ToString())) {
							await LogRequestAsync(context,correlationId);

							// Capture original response body stream
							var originalBodyStream = context.Response.Body;
							using var responseBody = new MemoryStream();
							context.Response.Body = responseBody;
							try {
								await next(context);
							}
							catch (Exception ex) {
								stopwatch.Stop();
								logger.LogError(ex,
												"Request failed for {RequestMethod} {RequestPath} with correlation ID {CorrelationId}. Duration: {Duration}ms",
												context.Request.Method,
												context.Request.Path,
												correlationId,
												stopwatch.ElapsedMilliseconds);
								throw;
							}
							stopwatch.Stop();
							await LogResponseAsync(context,correlationId,stopwatch.ElapsedMilliseconds);

							// Copy response body back to original stream
							await responseBody.CopyToAsync(originalBodyStream);
						}
	}

	private async Task LogRequestAsync(HttpContext context,string correlationId) {
		var request = context.Request;
		var requestBody = string.Empty;
		if (request.ContentLength > 0 &&
			request.ContentType?.Contains("application/json") == true) {
			request.EnableBuffering();
			using var reader = new StreamReader(request.Body,Encoding.UTF8,leaveOpen:true);
			requestBody = await reader.ReadToEndAsync();
			request.Body.Position = 0;
		}
		logger.LogInformation("Incoming request: {RequestMethod} {RequestPath} with correlation ID {CorrelationId}. " + "Content-Type: {ContentType}, Content-Length: {ContentLength}, Body: {RequestBody}",
							  request.Method,
							  request.Path + request.QueryString,
							  correlationId,
							  request.ContentType ?? "N/A",
							  request.ContentLength ?? 0,
							  !string.IsNullOrEmpty(requestBody) ? requestBody : "N/A");
	}

	private async Task LogResponseAsync(HttpContext context,
										string correlationId,
										long durationMs) {
		var response = context.Response;
		var responseBody = string.Empty;
		if (response.Body.CanSeek &&
			response.Body.Length > 0) {
			response.Body.Seek(0,SeekOrigin.Begin);
			using var reader = new StreamReader(response.Body,Encoding.UTF8,leaveOpen:true);
			responseBody = await reader.ReadToEndAsync();
			response.Body.Seek(0,SeekOrigin.Begin);
		}
		var logLevel = response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
		logger.Log(logLevel,
				   "Outgoing response: {RequestMethod} {RequestPath} returned {StatusCode} with correlation ID {CorrelationId}. " + "Duration: {Duration}ms, Content-Type: {ContentType}, Body: {ResponseBody}",
				   context.Request.Method,
				   context.Request.Path,
				   response.StatusCode,
				   correlationId,
				   durationMs,
				   response.ContentType ?? "N/A",
				   !string.IsNullOrEmpty(responseBody) ? responseBody : "N/A");
	}
}
