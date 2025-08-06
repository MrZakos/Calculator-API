using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IO.Swagger.Middleware {
	/// <summary>
	/// Middleware to validate JWT token expiration
	/// </summary>
	/// <param name="next"></param>
	/// <param name="logger"></param>
	public class TokenExpirationMiddleware(RequestDelegate next,ILogger<TokenExpirationMiddleware> logger) {

		/// <summary>
		/// Middleware to check if the JWT token is expired or about to expire
		/// </summary>
		/// <param name="context"></param>
		public async Task InvokeAsync(HttpContext context) {
			// Check if the user is authenticated and has a JWT token
			if (context.User.Identity?.IsAuthenticated == true) {
				var expClaim = context.User.FindFirst(JwtRegisteredClaimNames.Exp);
				if (expClaim != null) {
					// Convert Unix timestamp to DateTime
					var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim.Value));
					var currentTime = DateTimeOffset.UtcNow;
					if (exp <= currentTime) {
						logger.LogWarning("Token has expired for user. Expiration: {Expiration}, Current: {Current}",exp,currentTime);

						// Clear the authentication
						context.User = new ClaimsPrincipal();

						// Return 401 Unauthorized with custom message
						context.Response.StatusCode = 401;
						context.Response.ContentType = "application/json";
						var response = new {
							error = "token_expired",
							message = "The access token has expired. Please refresh your token or re-authenticate.",
							expired_at = exp.ToString("yyyy-MM-ddTHH:mm:ssZ")
						};
						await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
						return;
					}

					// Log warning if token expires soon (within 5 minutes)
					var timeUntilExpiration = exp - currentTime;
					if (timeUntilExpiration.TotalMinutes is <= 5 and > 0) {
						logger.LogInformation("Token expires soon for user. Time remaining: {TimeRemaining} minutes",Math.Round(timeUntilExpiration.TotalMinutes,2));
						// Add header to indicate token expires soon
						context.Response.Headers.Append("X-Token-Expires-In",Math.Round(timeUntilExpiration.TotalSeconds).ToString());
					}
				}
			}
			await next(context);
		}
	}

	/// <summary>
	///  Extension methods for TokenExpirationMiddleware
	/// </summary>
	public static class TokenExpirationMiddlewareExtensions {
		/// <summary>
		///  Extension method to add TokenExpirationMiddleware to the request pipeline
		/// </summary>
		/// <param name="builder"></param>
		/// <returns></returns>
		public static IApplicationBuilder UseTokenExpirationValidation(this IApplicationBuilder builder) {
			return builder.UseMiddleware<TokenExpirationMiddleware>();
		}
	}
}
