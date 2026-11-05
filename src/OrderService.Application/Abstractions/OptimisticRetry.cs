using OrderService.Domain.Common;

namespace OrderService.Application.Abstractions;

// On retry the operation sees the state committed by the competing request.
internal static class OptimisticRetry
{
    private const int MaxAttempts = 3;

    public static async Task<Result<T>> ExecuteAsync<T>(IUnitOfWork unitOfWork, Func<Task<Result<T>>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                unitOfWork.Reset();
            }
        }
    }
}
