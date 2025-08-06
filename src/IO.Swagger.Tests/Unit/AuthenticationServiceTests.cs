using IO.Swagger.Models.Token;
using IO.Swagger.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IO.Swagger.Tests.Unit;

public class AuthenticationServiceTests {
	private readonly AuthenticationService _authService;

	public AuthenticationServiceTests() {
		var configurationBuilder = new ConfigurationBuilder();
		configurationBuilder.AddInMemoryCollection(new Dictionary<string,string?> {
			{ "Jwt:Key","ThisIsASecretKeyForJWTTokenGenerationWithMinimum256BitsLength" },
			{ "Jwt:Issuer","TestIssuer" },
			{ "Jwt:Audience","TestAudience" },
			{ "Jwt:ExpiryInMinutes","1" }
		});
		var configuration = configurationBuilder.Build();
		_authService = new AuthenticationService(configuration);
	}

	[Theory]
	[InlineData("testuser")]
	[InlineData("admin")]
	[InlineData("user123")]
	public void GenerateToken_ValidUsername_ReturnsValidToken(string username) {
		// Arrange
		var request = new TokenRequest {
			Username = username
		};

		// Act
		var result = _authService.GenerateToken(request);

		// Assert
		Assert.NotNull(result);
		Assert.NotNull(result.Token);
		Assert.Equal("Bearer",result.TokenType);
		Assert.Equal(username,result.Username);
		Assert.True(result.ExpiresAt > DateTime.UtcNow);
		Assert.True(_authService.ValidateToken(result.Token));
	}

	[Theory]
	[InlineData("")]
	[InlineData("invalid.token.here")]
	[InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid")]
	[InlineData(null)]
	public void ValidateToken_InvalidToken_ReturnsFalse(string? token) {
		// Act
		var isValid = _authService.ValidateToken(token);

		// Assert
		Assert.False(isValid);
	}

	[Fact]
	public void ValidateToken_ExpiredToken_ReturnsFalse() {
		// Arrange - Create configuration with very short expiry
		var shortExpiryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
															  { "Jwt:Key","ThisIsASecretKeyForJWTTokenGenerationWithMinimum256BitsLength" },
															  { "Jwt:Issuer","TestIssuer" },
															  { "Jwt:Audience","TestAudience" },
															  { "Jwt:ExpiryInMinutes","0" } // Expires immediately
														  })
														  .Build();
		var shortExpiryService = new AuthenticationService(shortExpiryConfig);
		var request = new TokenRequest {
			Username = "testuser"
		};
		var tokenResponse = shortExpiryService.GenerateToken(request);

		// Wait a moment to ensure expiry
		Thread.Sleep(1000);

		// Act
		var isValid = _authService.ValidateToken(tokenResponse.Token);

		// Assert
		Assert.False(isValid);
	}

	[Theory]
	[InlineData("WrongKey")]
	[InlineData("DifferentIssuer")]
	[InlineData("DifferentAudience")]
	public void ValidateToken_WrongConfiguration_ReturnsFalse(string configType) {
		// Arrange - Create token with original config
		var request = new TokenRequest {
			Username = "testuser"
		};
		var tokenResponse = _authService.GenerateToken(request);

		// Create service with wrong configuration
		var wrongConfig = new Dictionary<string,string?> {
			{ "Jwt:Key","ThisIsASecretKeyForJWTTokenGenerationWithMinimum256BitsLength" },
			{ "Jwt:Issuer","TestIssuer" },
			{ "Jwt:Audience","TestAudience" },
			{ "Jwt:ExpiryInMinutes","30" }
		};
		switch (configType) {
			case "WrongKey":
				wrongConfig["Jwt:Key"] = "DifferentSecretKeyForJWTTokenGenerationWith256BitsLength";
				break;
			case "DifferentIssuer":
				wrongConfig["Jwt:Issuer"] = "WrongIssuer";
				break;
			case "DifferentAudience":
				wrongConfig["Jwt:Audience"] = "WrongAudience";
				break;
		}
		var wrongConfigBuilder = new ConfigurationBuilder().AddInMemoryCollection(wrongConfig);
		var wrongAuthService = new AuthenticationService(wrongConfigBuilder.Build());

		// Act
		var isValid = wrongAuthService.ValidateToken(tokenResponse.Token);

		// Assert
		Assert.False(isValid);
	}
}
