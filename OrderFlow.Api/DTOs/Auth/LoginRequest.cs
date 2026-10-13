
using System.ComponentModel.DataAnnotations;

namespace OrderFlow.Api.DTOs.Auth;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}