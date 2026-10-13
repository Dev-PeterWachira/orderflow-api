
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.DTOs.Auth;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public class AuthService : IAuthService
{
    private readonly OrderFlowDbContext _dbContext;
    private readonly IPasswordHasher<Customer> _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(
        OrderFlowDbContext dbContext,
        IPasswordHasher<Customer> passwordHasher,
        JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);

        var emailExists = await _dbContext.Customers
            .AnyAsync(c => c.Email == email);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Email = email,
            Role = "Customer"
        };

        // Store only the password hash, never the plaintext password.
        customer.PasswordHash = _passwordHasher.HashPassword(
            customer,
            request.Password);

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        return _jwtTokenService.CreateToken(customer);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);

        var customer = await _dbContext.Customers
            .SingleOrDefaultAsync(c => c.Email == email);

        // Also reject legacy accounts without a usable password hash.
        if (customer is null ||
            string.IsNullOrWhiteSpace(customer.PasswordHash))
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var result = _passwordHasher.VerifyHashedPassword(
            customer,
            customer.PasswordHash,
            request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        // Upgrade the stored hash if the hasher's format changes.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            customer.PasswordHash = _passwordHasher.HashPassword(
                customer,
                request.Password);

            await _dbContext.SaveChangesAsync();
        }

        return _jwtTokenService.CreateToken(customer);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();
}