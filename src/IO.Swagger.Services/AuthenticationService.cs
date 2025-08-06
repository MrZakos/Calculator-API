using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IO.Swagger.Models.Token;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IO.Swagger.Services {
	public interface IAuthenticationService {
		TokenResponse GenerateToken(TokenRequest request);
		bool ValidateToken(string token);
	}

	public class AuthenticationService(IConfiguration configuration) : IAuthenticationService {

		public TokenResponse GenerateToken(TokenRequest request) {
			if (request == null ||
				string.IsNullOrEmpty(request.Username)) {
				throw new ArgumentException("Username is required");
			}
			try {
				var jwtSettings = configuration.GetSection("Jwt");
				var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
				var credentials = new SigningCredentials(key,SecurityAlgorithms.HmacSha256);
				var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub,request.Username),new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),new Claim(JwtRegisteredClaimNames.Iat,DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),ClaimValueTypes.Integer64),new Claim(ClaimTypes.Name,request.Username),new Claim(ClaimTypes.NameIdentifier,request.Username) };
				var token = new JwtSecurityToken(issuer:jwtSettings["Issuer"],
												 audience:jwtSettings["Audience"],
												 claims:claims,
												 expires:DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["ExpiryInMinutes"])),
												 signingCredentials:credentials);
				var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
				return new TokenResponse {
					Token = tokenString,
					ExpiresAt = token.ValidTo,
					TokenType = "Bearer",
					Username = request.Username
				};
			}
			catch (Exception ex) {
				throw new InvalidOperationException($"Error generating token: {ex.Message}",ex);
			}
		}

		public bool ValidateToken(string token) {
			if (string.IsNullOrEmpty(token)) {
				return false;
			}
			try {
				var jwtSettings = configuration.GetSection("Jwt");
				var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
				var tokenHandler = new JwtSecurityTokenHandler();
				var validationParameters = new TokenValidationParameters {
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = key,
					ValidateIssuer = true,
					ValidIssuer = jwtSettings["Issuer"],
					ValidateAudience = true,
					ValidAudience = jwtSettings["Audience"],
					ValidateLifetime = true,
					ClockSkew = TimeSpan.Zero
				};
				tokenHandler.ValidateToken(token,validationParameters,out _);
				return true;
			}
			catch {
				return false;
			}
		}
	}
}
