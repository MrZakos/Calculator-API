namespace IO.Swagger.Models.Token;

/// <summary>
/// Represents a request to generate a JWT token for authentication purposes.
/// </summary>
public class TokenRequest {
	public string Username { get; set; } = "test";
}
