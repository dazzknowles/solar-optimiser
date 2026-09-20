using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolarOptimiser.Collection;

namespace SolarOptimiser.Host
{
    /// <summary>
    /// SOL-T-801: a single <see cref="BackgroundService"/>, using <see cref="PeriodicTimer"/>, polling on
    /// <see cref="CollectionOptions.PollInterval"/>. The loop skips a tick outright rather than overlapping if the
    /// previous poll is still running.
    /// </summary>
    public sealed class CollectionPollingService : BackgroundService
    {
        private readonly ICollectionRunner _collectionRunner;
        private readonly CollectionOptions _collectionOptions;
        private readonly ILogger<CollectionPollingService> _logger;
        private int _pollInProgress;

        public CollectionPollingService(
            ICollectionRunner collectionRunner,
            IOptions<CollectionOptions> collectionOptions,
            ILogger<CollectionPollingService> logger)
        {
            _collectionRunner = collectionRunner;
            _collectionOptions = collectionOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (PeriodicTimer timer = new PeriodicTimer(_collectionOptions.PollInterval))
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    if (Interlocked.CompareExchange(ref _pollInProgress, 1, 0) != 0)
                    {
                        _logger.LogWarning("Skipping this poll tick because the previous poll is still running.");
                        continue;
                    }

                    try
                    {
                        await _collectionRunner.RunOnceAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Unhandled error during a collection poll.");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _pollInProgress, 0);
                    }
                }
            }
        }
    }
}
