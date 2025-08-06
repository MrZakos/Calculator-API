namespace IO.Swagger.Models.Token;

/// <summary>
///  Represents the response containing a JWT token and its metadata.
/// </summary>
public class TokenResponse {
	public string Token { get; set; }
	public DateTime ExpiresAt { get; set; }
	public string TokenType { get; set; }
	public string Username { get; set; }
}
