
namespace OrderFlow.Api.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public int CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}