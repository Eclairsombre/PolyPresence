using System.Collections.Concurrent;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class RateLimitServiceTests
{
    [Fact]
    public void IsAllowed_ShouldAllowWithinLimit()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);

        var first = service.IsAllowed("k1", 2, TimeSpan.FromMinutes(1));
        var second = service.IsAllowed("k1", 2, TimeSpan.FromMinutes(1));

        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ShouldBlockOverLimit()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);

        service.IsAllowed("k2", 1, TimeSpan.FromMinutes(1)).Should().BeTrue();
        service.IsAllowed("k2", 1, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void IsLoginAttemptAllowed_ShouldBlockAfterThreshold()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);

        for (var i = 0; i < 5; i++)
        {
            service.IsLoginAttemptAllowed("std01").Should().BeTrue();
        }

        service.IsLoginAttemptAllowed("std01").Should().BeFalse();
    }

    [Fact]
    public void IsPasswordResetAllowed_ShouldBlockAfterThreshold()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);

        service.IsPasswordResetAllowed("mail@test").Should().BeTrue();
        service.IsPasswordResetAllowed("mail@test").Should().BeTrue();
        service.IsPasswordResetAllowed("mail@test").Should().BeFalse();
    }

    [Fact]
    public void IsApiCallAllowed_ShouldBlockAfterThreshold()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);

        for (var i = 0; i < 500; i++)
        {
            service.IsApiCallAllowed("u1").Should().BeTrue();
        }

        service.IsApiCallAllowed("u1").Should().BeFalse();
    }

    [Fact]
    public void ResetLoginAttempts_ShouldResetCounter()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);

        for (var i = 0; i < 6; i++)
        {
            service.IsLoginAttemptAllowed("std02");
        }

        service.ResetLoginAttempts("std02");

        service.IsLoginAttemptAllowed("std02").Should().BeTrue();
    }

    [Fact]
    public void CleanupExpiredEntries_ShouldRemoveOldEntries()
    {
        var service = new RateLimitService(NullLogger<RateLimitService>.Instance);
        var requestsField = typeof(RateLimitService)
            .GetField("_requests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        requestsField.Should().NotBeNull();

        var requests = (ConcurrentDictionary<string, List<DateTime>>)requestsField!.GetValue(service)!;
        requests["old:key"] = new List<DateTime> { DateTime.UtcNow.AddHours(-25) };

        service.CleanupExpiredEntries();

        requests.ContainsKey("old:key").Should().BeFalse();
    }
}
