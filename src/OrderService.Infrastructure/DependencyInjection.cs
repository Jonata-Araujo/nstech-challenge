using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Abstractions;
using OrderService.Application.Auth;
using OrderService.Infrastructure.Auth;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Repositories;

namespace OrderService.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "OrdersDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.")));

        services.AddSingleton<TimeProvider, DatabasePrecisionTimeProvider>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrderDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must have at least 32 characters.")
            .Validate(o => o.ExpirationMinutes > 0, "Jwt:ExpirationMinutes must be positive.")
            .ValidateOnStart();

        services.AddOptions<AuthUsersOptions>()
            .Bind(configuration.GetSection(AuthUsersOptions.SectionName));

        services.AddSingleton<IUserCredentialValidator, ConfiguredUserCredentialValidator>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
