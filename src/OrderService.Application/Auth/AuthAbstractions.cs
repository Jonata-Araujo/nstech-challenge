namespace OrderService.Application.Auth;

public sealed record AuthenticatedUser(string Username, string Role, Guid? CustomerId);

public sealed record TokenDto(string AccessToken, string TokenType, int ExpiresIn);

public interface IUserCredentialValidator
{
    Task<AuthenticatedUser?> ValidateAsync(string username, string password, CancellationToken cancellationToken);
}

public interface ITokenIssuer
{
    TokenDto Issue(AuthenticatedUser user);
}
