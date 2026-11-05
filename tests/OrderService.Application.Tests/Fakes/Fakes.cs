using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Application.Orders.ListOrders;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;
using OrderService.Domain.Products;

namespace OrderService.Application.Tests.Fakes;

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    private FakeCurrentUser(string username, Guid? customerId, bool isAdmin)
    {
        Username = username;
        CustomerId = customerId;
        IsAdmin = isAdmin;
    }

    public string? Username { get; }

    public Guid? CustomerId { get; }

    public bool IsAdmin { get; }

    public static FakeCurrentUser Admin() => new("admin", null, true);

    public static FakeCurrentUser Customer(Guid customerId) => new("customer", customerId, false);
}

internal sealed class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = [];

    public Product Add(decimal price = 10m, int stock = 10, string currency = "BRL")
    {
        var product = Product.Create(Guid.NewGuid(), "Product", price, Currency.FromCode(currency), stock);
        _products[product.Id] = product;
        return product;
    }

    public Task<IReadOnlyDictionary<Guid, Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Product>>(
            _products.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));
}

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = [];

    public IReadOnlyCollection<Order> All => _orders.Values;

    public Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.GetValueOrDefault(id));

    public void Add(Order order) => _orders[order.Id] = order;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private int _conflictsToThrow;

    public int SaveCount { get; private set; }

    public int ResetCount { get; private set; }

    public int CommittedTransactions { get; private set; }

    public int RolledBackTransactions { get; private set; }

    public void FailNextSavesWithConflict(int times) => _conflictsToThrow = times;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_conflictsToThrow > 0)
        {
            _conflictsToThrow--;
            throw new ConcurrencyConflictException("conflict");
        }

        SaveCount++;
        return Task.CompletedTask;
    }

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken)
    {
        var result = await operation(cancellationToken);
        if (result.IsSuccess) CommittedTransactions++;
        else RolledBackTransactions++;
        return result;
    }

    public void Reset() => ResetCount++;
}

internal sealed class FakeOrderReadRepository : IOrderReadRepository
{
    public OrderDto? Order { get; set; }

    public OrderListCriteria? LastCriteria { get; private set; }

    public Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Order?.Id == id ? Order : null);

    public Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListCriteria criteria, CancellationToken cancellationToken)
    {
        LastCriteria = criteria;
        return Task.FromResult(new PagedResult<OrderSummaryDto>([], criteria.Page, criteria.PageSize, 0));
    }
}
