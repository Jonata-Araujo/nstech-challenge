using Microsoft.Extensions.Options;
using OrderService.Application.Auth;

namespace OrderService.Infrastructure.Auth;

internal sealed class ConfiguredUserCredentialValidator(IOptions<AuthUsersOptions> options) : IUserCredentialValidator
{
    // Verified when the user does not exist, so response time does not reveal valid usernames.
    private static readonly string DummyHash = Pbkdf2PasswordHasher.Hash("dummy-password");

    public Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = options.Value.Users.FirstOrDefault(
            u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        var valid = Pbkdf2PasswordHasher.Verify(password, user?.PasswordHash ?? DummyHash);

        AuthenticatedUser? result = valid && user is not null
            ? new AuthenticatedUser(user.Username, user.Role, user.CustomerId)
            : null;

        return Task.FromResult(result);
    }
}
