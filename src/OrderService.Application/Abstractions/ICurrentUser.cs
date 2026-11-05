namespace OrderService.Application.Abstractions;

public interface ICurrentUser
{
    string? Username { get; }

    Guid? CustomerId { get; }

    bool IsAdmin { get; }
}

public static class CurrentUserExtensions
{
    public static bool CanAccessCustomer(this ICurrentUser user, Guid customerId) =>
        user.IsAdmin || (user.CustomerId is { } own && own == customerId);
}
