using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace IO.Swagger.Middleware;

/// <summary>
/// Middleware for monitoring API performance and resource usage
/// </summary>
public class PerformanceMonitoringMiddleware(RequestDelegate next,ILogger<PerformanceMonitoringMiddleware> logger) {
	private const int SlowRequestThresholdMs = 1000; // 1 second

	public async Task InvokeAsync(HttpContext context) {
		var stopwatch = Stopwatch.StartNew();
		var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault();

		// Monitor memory before request
		var memoryBefore = GC.GetTotalMemory(false);
		using (LogContext.PushProperty("CorrelationId",correlationId)) {
			try {
				await next(context);
			}
			finally {
				stopwatch.Stop();
				var memoryAfter = GC.GetTotalMemory(false);
				var memoryUsed = memoryAfter - memoryBefore;
				var performanceData = new {
					RequestPath = context.Request.Path.Value,
					Method = context.Request.Method,
					StatusCode = context.Response.StatusCode,
					DurationMs = stopwatch.ElapsedMilliseconds,
					MemoryUsedBytes = memoryUsed,
					MemoryUsedKB = Math.Round(memoryUsed / 1024.0,2),
					CorrelationId = correlationId
				};

				// Log performance metrics
				if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs) {
					logger.LogWarning("SLOW REQUEST detected: {Method} {RequestPath} took {DurationMs}ms " + "(threshold: {ThresholdMs}ms). Memory used: {MemoryUsedKB}KB. " + "Status: {StatusCode}, CorrelationId: {CorrelationId}",
									  performanceData.Method,
									  performanceData.RequestPath,
									  performanceData.DurationMs,
									  SlowRequestThresholdMs,
									  performanceData.MemoryUsedKB,
									  performanceData.StatusCode,
									  performanceData.CorrelationId);
				}
				else {
					logger.LogDebug("Performance metrics: {Method} {RequestPath} completed in {DurationMs}ms. " + "Memory used: {MemoryUsedKB}KB, Status: {StatusCode}, CorrelationId: {CorrelationId}",
									performanceData.Method,
									performanceData.RequestPath,
									performanceData.DurationMs,
									performanceData.MemoryUsedKB,
									performanceData.StatusCode,
									performanceData.CorrelationId);
				}

				// Add performance headers to response
				context.Response.Headers.Add("X-Response-Time",$"{stopwatch.ElapsedMilliseconds}ms");
				context.Response.Headers.Add("X-Memory-Used",$"{performanceData.MemoryUsedKB}KB");
			}
		}
	}
}
