using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using backend.Data;
using backend.Controllers;

namespace backend.Services
{
    public class TimerService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<TimerService> _logger;
        private System.Timers.Timer? _dailySessionTimer;
        private System.Timers.Timer? _sessionMailTimer;
        private System.Timers.Timer? _dailyMailTimer;
        private DateTime _nextSessionExecutionTime;
        private DateTime _nextMailExecutionTime;
        public static DateTime StaticNextSessionExecutionTime { get; private set; }
        public static DateTime StaticNextMailExecutionTime { get; private set; }
        public static bool IsAutoImportEnabled { get; private set; } = true;

        public TimerService(IServiceScopeFactory serviceScopeFactory, ILogger<TimerService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            InitDailySessionTimer();
            InitSessionMailTimer();
            InitDailyMailTimer();
        }

        private void InitDailySessionTimer()
        {
            if (!IsAutoImportEnabled)
            {
                _logger.LogInformation("Import automatique de l'EDT désactivé");
                return;
            }
            _logger.LogInformation("Initialisation du timer de synchronisation quotidienne des sessions et attendances à " +
                                   $"{Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_HOUR")}:" +
                                   $"{Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_MINUTE")}:" +
                                   $"{Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_SECOND")}");

            _nextSessionExecutionTime = GetNextSessionExecutionTime();
            StaticNextSessionExecutionTime = _nextSessionExecutionTime;
            _dailySessionTimer = new System.Timers.Timer((_nextSessionExecutionTime - DateTime.Now).TotalMilliseconds);
            _dailySessionTimer.Elapsed += async (sender, e) => await DailySessionSync();
            _dailySessionTimer.AutoReset = false;
            _dailySessionTimer.Start();
        }

        private void InitSessionMailTimer()
        {
            ScheduleSessionMailTimerTo8h();
        }

        private void ScheduleSessionMailTimerTo8h()
        {
            var now = DateTime.Now;
            var next8h = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);
            while (next8h.DayOfWeek == DayOfWeek.Saturday || next8h.DayOfWeek == DayOfWeek.Sunday)
                next8h = next8h.AddDays(1);
            if (now > next8h)
            {
                do
                {
                    next8h = next8h.AddDays(1);
                } while (next8h.DayOfWeek == DayOfWeek.Saturday || next8h.DayOfWeek == DayOfWeek.Sunday);
            }
            var msTo8h = (next8h - now).TotalMilliseconds;
            _logger.LogInformation($"Timer mail prof : prochaine fenêtre à 8h ({next8h}), dans {msTo8h / 1000 / 60:F1} min");
            _sessionMailTimer = new System.Timers.Timer(msTo8h);
            _sessionMailTimer.Elapsed += (sender, e) => StartSessionMail15MinTimer();
            _sessionMailTimer.AutoReset = false;
            _sessionMailTimer.Start();
        }

        private void StartSessionMail15MinTimer()
        {
            _logger.LogInformation("Démarrage du timer mail prof toutes les 15min (8h-19h)");
            var timer = new System.Timers.Timer(15 * 60 * 1000);
            timer.Elapsed += async (sender, e) =>
            {
                var now = DateTime.Now;
                if (now.Hour >= 8 && now.Hour < 19)
                {
                    await CheckAndSendSessionMails();
                }
                if (now.Hour >= 19 && now.Minute < 15)
                {
                    timer.Stop();
                    timer.Dispose();
                    _logger.LogInformation("Arrêt du timer mail prof à 19h. Replanification pour demain 8h.");
                    ScheduleSessionMailTimerTo8h();
                }
            };
            timer.AutoReset = true;
            timer.Start();
            _sessionMailTimer = timer;
        }

        private void InitDailyMailTimer()
        {
            _logger.LogInformation("Initialisation du timer d'envoi des feuilles de présence à l'administration");
            _nextMailExecutionTime = GetNextMailExecutionTime();
            StaticNextMailExecutionTime = _nextMailExecutionTime;
            _dailyMailTimer = new System.Timers.Timer((_nextMailExecutionTime - DateTime.Now).TotalMilliseconds);
            _logger.LogInformation($"Prochain envoi des feuilles de présence à {_nextMailExecutionTime}, dans {(_nextMailExecutionTime - DateTime.Now).TotalMinutes:F1} min");
            _dailyMailTimer.Elapsed += async (sender, e) => await SendDailyAttendanceSheets();
            _dailyMailTimer.AutoReset = false;
            _dailyMailTimer.Start();
        }

        private DateTime GetNextSessionExecutionTime()
        {
            var now = DateTime.Now;
            var target = new DateTime(
                now.Year,
                now.Month,
                now.Day,
                int.Parse(Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_HOUR") ?? "0"),
                int.Parse(Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_MINUTE") ?? "0"),
                int.Parse(Environment.GetEnvironmentVariable("EDT_IMPORT_TIME_SECOND") ?? "0")
            );
            if (now > target) target = target.AddDays(1);
            return target;
        }

        private DateTime GetNextMailExecutionTime()
        {
            var now = DateTime.Now;
            var target = new DateTime(
                now.Year,
                now.Month,
                now.Day,
                int.Parse(Environment.GetEnvironmentVariable("MAIL_SENT_HOUR") ?? "0"),
                int.Parse(Environment.GetEnvironmentVariable("MAIL_SENT_MINUTE") ?? "0"),
                int.Parse(Environment.GetEnvironmentVariable("MAIL_SENT_SECOND") ?? "0")
            );
            if (now > target) target = target.AddDays(1);
            return target;
        }

        private async Task DailySessionSync()
        {
            if (!IsAutoImportEnabled)
            {
                _logger.LogInformation("Import automatique désactivé, synchronisation annulée");
                return;
            }

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionController>>();
                var importLogger = scope.ServiceProvider.GetRequiredService<ILogger<ImportController>>();
                logger.LogInformation("Synchronisation quotidienne des sessions et attendances à 01:00");
                var controller = new ImportController(scopedContext, importLogger, _serviceScopeFactory);
                await controller.ImportAllIcsLinks(scopedContext, importLogger);
            }
            _nextSessionExecutionTime = GetNextSessionExecutionTime();
            StaticNextSessionExecutionTime = _nextSessionExecutionTime;
            if (_dailySessionTimer != null)
            {
                _dailySessionTimer.Interval = (_nextSessionExecutionTime - DateTime.Now).TotalMilliseconds;
                _dailySessionTimer.Start();
            }
        }

        private async Task CheckAndSendSessionMails()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionController>>();
                var controller = new SessionController(scopedContext, logger, _serviceScopeFactory);
                await controller.CheckAndSendSessionMails();
            }
        }

        private async Task SendDailyAttendanceSheets()
        {
            _logger.LogInformation("Envoi quotidien des feuilles de présence à l'administration");
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionController>>();
                var controller = new MailPreferencesController(scopedContext, logger, _serviceScopeFactory);
                await controller.GenerateAndSendZip();
            }
            _nextMailExecutionTime = GetNextMailExecutionTime();
            StaticNextMailExecutionTime = _nextMailExecutionTime;
            if (_dailyMailTimer != null)
            {
                _dailyMailTimer.Interval = (_nextMailExecutionTime - DateTime.Now).TotalMilliseconds;
                _logger.LogInformation($"Prochain envoi des feuilles de présence à {_nextMailExecutionTime}, dans {(_nextMailExecutionTime - DateTime.Now).TotalMinutes:F1} min");
                _dailyMailTimer.Start();
            }
        }

        public static void EnableAutoImport(ILogger logger)
        {
            IsAutoImportEnabled = true;
            logger.LogInformation("Import automatique de l'EDT activé");
        }

        public static void DisableAutoImport(ILogger logger)
        {
            IsAutoImportEnabled = false;
            logger.LogInformation("Import automatique de l'EDT désactivé");
        }
    }
}
