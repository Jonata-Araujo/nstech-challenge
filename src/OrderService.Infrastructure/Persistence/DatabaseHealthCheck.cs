using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderService.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(OrderDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database is unreachable.");
}
