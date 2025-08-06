using System;
using IO.Swagger.Models.Token;
using IO.Swagger.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace IO.Swagger.Controllers {
	/// <summary>
	/// Authentication Controller for generating JWT tokens for demo purposes
	/// </summary>
	/// <param name="authenticationService"></param>
	[ApiController]
	[Route("api/token")]
	public class AuthController(IAuthenticationService authenticationService) : ControllerBase {

		/// <summary>
		/// Generate a JWT token for API authentication
		/// </summary>
		/// <param name="request">Token generation request containing username and optional claims</param>
		/// <returns>JWT Bearer token with expiration information</returns>
		[HttpPost("generate")]
		[SwaggerOperation("GenerateToken")]
		[SwaggerResponse(statusCode:200,description:"Token generated successfully")]
		[SwaggerResponse(statusCode:400,description:"Invalid request")]
		public IActionResult GenerateToken([FromBody] TokenRequest request) {
			try {
				var tokenResponse = authenticationService.GenerateToken(request);
				return Ok(tokenResponse);
			}
			catch (ArgumentException ex) {
				return BadRequest(ex.Message);
			}
			catch (InvalidOperationException ex) {
				return BadRequest(ex.Message);
			}
		}
	}
}
