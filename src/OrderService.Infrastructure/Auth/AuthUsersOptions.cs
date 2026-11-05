namespace OrderService.Infrastructure.Auth;

public sealed class AuthUsersOptions
{
    public const string SectionName = "Auth";

    public List<ConfiguredUser> Users { get; init; } = [];
}

public sealed class ConfiguredUser
{
    public string Username { get; init; } = string.Empty;

    // Format: PBKDF2-SHA256$iterations$saltBase64$hashBase64
    public string PasswordHash { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid? CustomerId { get; init; }
}
