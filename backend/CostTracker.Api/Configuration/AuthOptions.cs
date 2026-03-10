namespace CostTracker.Api.Configuration;

public sealed class AuthOptions
{
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
}
