using System.Reflection;
using System.Runtime.Serialization;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class TimerServiceTests
{
    [Fact]
    public void GetNextSessionExecutionTime_ShouldReturnFutureDate()
    {
        var now = DateTime.Now;
        var target = now.AddMinutes(-1);

        var oldHour = Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_HOUR");
        var oldMinute = Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_MINUTE");
        var oldSecond = Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_SECOND");

        try
        {
            Environment.SetEnvironmentVariable("EDT_IMPORT_TIME_HOUR", target.Hour.ToString());
            Environment.SetEnvironmentVariable("EDT_IMPORT_TIME_MINUTE", target.Minute.ToString());
            Environment.SetEnvironmentVariable("EDT_IMPORT_TIME_SECOND", target.Second.ToString());

            var service = CreateUninitializedTimerService();
            var method = typeof(TimerService).GetMethod("GetNextSessionExecutionTime", BindingFlags.NonPublic | BindingFlags.Instance);

            method.Should().NotBeNull();
            var next = (DateTime)method!.Invoke(service, null)!;

            next.Should().BeAfter(DateTime.Now.AddSeconds(-1));
            next.Should().BeBefore(DateTime.Now.AddDays(2));
        }
        finally
        {
            Environment.SetEnvironmentVariable("EDT_IMPORT_TIME_HOUR", oldHour);
            Environment.SetEnvironmentVariable("EDT_IMPORT_TIME_MINUTE", oldMinute);
            Environment.SetEnvironmentVariable("EDT_IMPORT_TIME_SECOND", oldSecond);
        }
    }

    [Fact]
    public void GetNextMailExecutionTime_ShouldReturnFutureDate()
    {
        var now = DateTime.Now;
        var target = now.AddMinutes(-1);

        var oldHour = Environment.GetEnvironmentVariable("MAIL_SENT_HOUR");
        var oldMinute = Environment.GetEnvironmentVariable("MAIL_SENT_MINUTE");
        var oldSecond = Environment.GetEnvironmentVariable("MAIL_SENT_SECOND");

        try
        {
            Environment.SetEnvironmentVariable("MAIL_SENT_HOUR", target.Hour.ToString());
            Environment.SetEnvironmentVariable("MAIL_SENT_MINUTE", target.Minute.ToString());
            Environment.SetEnvironmentVariable("MAIL_SENT_SECOND", target.Second.ToString());

            var service = CreateUninitializedTimerService();
            var method = typeof(TimerService).GetMethod("GetNextMailExecutionTime", BindingFlags.NonPublic | BindingFlags.Instance);

            method.Should().NotBeNull();
            var next = (DateTime)method!.Invoke(service, null)!;

            next.Should().BeAfter(DateTime.Now.AddSeconds(-1));
            next.Should().BeBefore(DateTime.Now.AddDays(2));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAIL_SENT_HOUR", oldHour);
            Environment.SetEnvironmentVariable("MAIL_SENT_MINUTE", oldMinute);
            Environment.SetEnvironmentVariable("MAIL_SENT_SECOND", oldSecond);
        }
    }

    [Fact]
    public void InitDailySessionTimer_ShouldNotCreateTimer_WhenAutoImportDisabled()
    {
        var service = CreateUninitializedTimerService();

        TimerService.DisableAutoImport(NullLogger.Instance);
        try
        {
            var method = typeof(TimerService).GetMethod("InitDailySessionTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should().NotBeNull();

            method!.Invoke(service, null);

            var timerField = typeof(TimerService).GetField("_dailySessionTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            timerField.Should().NotBeNull();
            timerField!.GetValue(service).Should().BeNull();
        }
        finally
        {
            TimerService.EnableAutoImport(NullLogger.Instance);
        }
    }

    private static TimerService CreateUninitializedTimerService()
    {
#pragma warning disable SYSLIB0050
        var service = (TimerService)FormatterServices.GetUninitializedObject(typeof(TimerService));
#pragma warning restore SYSLIB0050

        typeof(TimerService)
            .GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, NullLogger<TimerService>.Instance);

        return service;
    }
}
