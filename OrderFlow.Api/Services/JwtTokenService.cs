
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Api.DTOs.Auth;
using OrderFlow.Api.Models;
using Claim = System.Security.Claims.Claim;

namespace OrderFlow.Api.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AuthResponse CreateToken(Customer customer)
    {
        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is missing.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is missing.");

        var key = _configuration["JWT_KEY"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");

        var expirationMinutes =
            _configuration.GetValue<int>("Jwt:ExpirationMinutes");

        if (expirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiration must be greater than zero.");
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                customer.Id.ToString()),

            new Claim(ClaimTypes.Role, customer.Role)
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt,
            CustomerId = customer.Id,
            Name = customer.Name,
            Role = customer.Role
        };
    }
}