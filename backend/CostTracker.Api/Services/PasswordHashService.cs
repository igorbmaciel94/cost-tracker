using Microsoft.AspNetCore.Identity;

namespace CostTracker.Api.Services;

public class PasswordHashService
{
    private readonly PasswordHasher<string> _passwordHasher = new();

    public string HashPassword(string username, string password)
    {
        return _passwordHasher.HashPassword(username, password);
    }

    public bool VerifyPassword(string username, string passwordHash, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(username, passwordHash, password);
        return result != PasswordVerificationResult.Failed;
    }
}
