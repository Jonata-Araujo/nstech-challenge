namespace OrderService.Api.Contracts;

public sealed record TokenRequest(string? Username, string? Password);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);

public sealed record CreateOrderRequest(Guid CustomerId, string? Currency, IReadOnlyCollection<CreateOrderItemRequest>? Items);

public sealed record ListOrdersRequest(
    Guid? CustomerId,
    string? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Page,
    int? PageSize);
