using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application;
using OrderService.Application.Abstractions;
using OrderService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OrderService.Infrastructure.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "ORDERS_TEST_CONNECTION_STRING";

    private PostgreSqlContainer? _container;

    public IServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrdersDb"] = connectionString,
                ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, AdminUser>();
        Services = services.BuildServiceProvider(validateScopes: true);

        await Services.ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is IAsyncDisposable disposable)
            await disposable.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();
    }

    private sealed class AdminUser : ICurrentUser
    {
        public string? Username => "admin";

        public Guid? CustomerId => null;

        public bool IsAdmin => true;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresFixture.ConnectionStringVariable))
            && !DockerIsAvailable())
        {
            Skip = $"Requires Docker or {PostgresFixture.ConnectionStringVariable}.";
        }
    }

    private static bool DockerIsAvailable() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"))
        || (OperatingSystem.IsWindows()
            ? File.Exists(@"\\.\pipe\docker_engine") || File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine")
            : File.Exists("/var/run/docker.sock"));
}
