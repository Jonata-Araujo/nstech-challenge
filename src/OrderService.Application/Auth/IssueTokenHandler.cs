using OrderService.Domain.Common;

namespace OrderService.Application.Auth;

public sealed record IssueTokenCommand(string? Username, string? Password);

public sealed class IssueTokenHandler(IUserCredentialValidator credentialValidator, ITokenIssuer tokenIssuer)
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Invalid username or password.");

    public async Task<Result<TokenDto>> HandleAsync(IssueTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrEmpty(command.Password))
            return Error.Validation("auth.credentials_required", "Username and password are required.");

        var user = await credentialValidator.ValidateAsync(command.Username, command.Password, cancellationToken);

        return user is null ? InvalidCredentials : tokenIssuer.Issue(user);
    }
}
