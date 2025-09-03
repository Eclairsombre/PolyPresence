using System.Collections.Concurrent;

namespace backend.Services
{
    public interface IRateLimitService
    {
        bool IsAllowed(string key, int maxRequests, TimeSpan timeWindow);
        bool IsLoginAttemptAllowed(string identifier);
        bool IsPasswordResetAllowed(string identifier);
        bool IsApiCallAllowed(string userId);
        void ResetLoginAttempts(string identifier);
    }

    public class RateLimitService : IRateLimitService
    {
        private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();
        private readonly ILogger<RateLimitService> _logger;

        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

        private const int MaxPasswordResetAttempts = 2;
        private static readonly TimeSpan PasswordResetWindow = TimeSpan.FromHours(1);

        private const int MaxApiCalls = 100;
        private static readonly TimeSpan ApiCallWindow = TimeSpan.FromMinutes(1);

        public RateLimitService(ILogger<RateLimitService> logger)
        {
            _logger = logger;
        }

        public bool IsAllowed(string key, int maxRequests, TimeSpan timeWindow)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.Subtract(timeWindow);

            _requests.AddOrUpdate(key,
                new List<DateTime> { now },
                (k, requests) =>
                {
                    requests.RemoveAll(r => r < windowStart);
                    requests.Add(now);
                    return requests;
                });

            var currentCount = _requests[key].Count;
            var isAllowed = currentCount <= maxRequests;

            if (!isAllowed)
            {
                _logger.LogWarning("Rate limit exceeded for key {Key}. Current: {Current}, Max: {Max}",
                    key, currentCount, maxRequests);
            }

            return isAllowed;
        }

        public bool IsLoginAttemptAllowed(string identifier)
        {
            return IsAllowed($"login:{identifier}", MaxLoginAttempts, LoginWindow);
        }

        public bool IsPasswordResetAllowed(string identifier)
        {
            return IsAllowed($"password_reset:{identifier}", MaxPasswordResetAttempts, PasswordResetWindow);
        }

        public bool IsApiCallAllowed(string userId)
        {
            return IsAllowed($"api:{userId}", MaxApiCalls, ApiCallWindow);
        }

        public void ResetLoginAttempts(string identifier)
        {
            var key = $"login:{identifier}";
            _requests.TryRemove(key, out _);
            _logger.LogInformation("Reset login attempts for {Identifier}", identifier);
        }

        public void CleanupExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _requests)
            {
                kvp.Value.RemoveAll(r => r < now.AddHours(-24));

                if (kvp.Value.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _requests.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limit entries", keysToRemove.Count);
            }
        }
    }
}
