using OrderService.Application.Abstractions;
using OrderService.Application.Tests.Fakes;
using OrderService.Domain.Common;

namespace OrderService.Application.Tests;

public class OptimisticRetryTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Retries_and_resets_tracking_after_a_conflict()
    {
        var attempts = 0;

        var result = await OptimisticRetry.ExecuteAsync(_unitOfWork, () =>
        {
            attempts++;
            if (attempts == 1) throw new ConcurrencyConflictException("conflict");
            return Task.FromResult(Result.Success(42));
        });

        result.Value.Should().Be(42);
        attempts.Should().Be(2);
        _unitOfWork.ResetCount.Should().Be(1);
    }

    [Fact]
    public async Task Gives_up_after_three_attempts()
    {
        var attempts = 0;

        var act = () => OptimisticRetry.ExecuteAsync<int>(_unitOfWork, () =>
        {
            attempts++;
            throw new ConcurrencyConflictException("conflict");
        });

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Does_not_retry_business_failures()
    {
        var attempts = 0;

        var result = await OptimisticRetry.ExecuteAsync(_unitOfWork, () =>
        {
            attempts++;
            return Task.FromResult(Result.Failure<int>(Error.Conflict("x", "y")));
        });

        result.IsFailure.Should().BeTrue();
        attempts.Should().Be(1);
    }
}
