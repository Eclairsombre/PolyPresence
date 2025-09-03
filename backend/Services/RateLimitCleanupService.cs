using backend.Services;

namespace backend.Services
{
    public class RateLimitCleanupService : BackgroundService
    {
        private readonly IRateLimitService _rateLimitService;
        private readonly ILogger<RateLimitCleanupService> _logger;

        public RateLimitCleanupService(IRateLimitService rateLimitService, ILogger<RateLimitCleanupService> logger)
        {
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_rateLimitService is RateLimitService rateLimitServiceImpl)
                    {
                        rateLimitServiceImpl.CleanupExpiredEntries();
                    }

                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during rate limit cleanup");

                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }
    }
}
