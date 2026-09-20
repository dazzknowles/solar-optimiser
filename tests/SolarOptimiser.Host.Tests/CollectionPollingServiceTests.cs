using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarOptimiser.Collection;

namespace SolarOptimiser.Host.Tests;

[TestClass]
public sealed class CollectionPollingServiceTests
{
    [TestMethod]
    public async Task SlowPoll_NeverOverlapsWithAnotherTick()
    {
        BlockingRunner runner = new BlockingRunner();
        CollectionOptions options = new CollectionOptions { PollInterval = TimeSpan.FromMilliseconds(20) };
        CollectionPollingService service = new CollectionPollingService(
            runner,
            Options.Create(options),
            NullLogger<CollectionPollingService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await runner.FirstPollStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(120);
            Assert.AreEqual(1, runner.CallCount);
            Assert.AreEqual(1, runner.MaximumConcurrency);

            runner.ReleaseFirstPoll.TrySetResult();
            await runner.SecondPollStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, runner.MaximumConcurrency);
        }
        finally
        {
            runner.ReleaseFirstPoll.TrySetResult();
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    private sealed class BlockingRunner : ICollectionRunner
    {
        private int _active;
        private int _callCount;
        private int _maximumConcurrency;

        public TaskCompletionSource FirstPollStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondPollStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstPoll { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            int active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            int call = Interlocked.Increment(ref _callCount);
            try
            {
                if (call == 1)
                {
                    FirstPollStarted.TrySetResult();
                    await ReleaseFirstPoll.Task.WaitAsync(cancellationToken);
                }
                else if (call == 2)
                {
                    SecondPollStarted.TrySetResult();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            int current;
            do
            {
                current = Volatile.Read(ref _maximumConcurrency);
                if (candidate <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref _maximumConcurrency, candidate, current) != current);
        }
    }
}
