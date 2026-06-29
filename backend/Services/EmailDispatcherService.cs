using System.Net;
using System.Net.Mail;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    /// <summary>
    /// Worker d'arrière-plan qui vide la file d'emails (table OutboxEmails).
    /// Les contrôleurs déposent un email (insertion rapide), ce service l'envoie
    /// par lots avec retry/backoff exponentiel — sans bloquer les requêtes ni le cron.
    /// </summary>
    public class EmailDispatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailDispatcherService> _logger;
        private readonly bool _sendingEnabled;

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
        private const int BatchSize = 25;
        private const int MaxAttempts = 5;

        public EmailDispatcherService(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailDispatcherService> logger,
            IHostEnvironment environment)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            // SÉCURITÉ : on n'envoie réellement les emails QU'EN PRODUCTION.
            // Hors production (dev/test), mode "dry-run" : les emails sont loggés et
            // marqués comme traités, mais JAMAIS envoyés par SMTP.
            // Échappatoire explicite si besoin : variable d'env SMTP_FORCE_SEND=true.
            _sendingEnabled = environment.IsProduction()
                || string.Equals(
                    Environment.GetEnvironmentVariable("SMTP_FORCE_SEND"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "EmailDispatcherService démarré (intervalle {Interval}s, envoi réel: {Enabled}).",
                PollInterval.TotalSeconds,
                _sendingEnabled);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur dans la boucle d'envoi d'emails.");
                }

                try { await Task.Delay(PollInterval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task ProcessBatchAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;
            var pending = await db.OutboxEmails
                .Where(e => e.Status == OutboxEmailStatus.Pending && e.NextAttemptAt <= now)
                .OrderBy(e => e.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (pending.Count == 0) return;

            // Hors production : on ne contacte jamais le serveur SMTP.
            if (!_sendingEnabled)
            {
                foreach (var email in pending)
                {
                    _logger.LogInformation("[DRY-RUN] Email NON envoyé (hors production) -> {To} : {Subject}", email.ToEmail, email.Subject);
                    email.Status = OutboxEmailStatus.Sent;
                    email.SentAt = DateTime.UtcNow;
                    email.LastError = "DRY-RUN : envoi désactivé hors production";
                }
                await db.SaveChangesAsync(ct);
                return;
            }

            _logger.LogInformation("Envoi de {Count} email(s) en attente.", pending.Count);

            SmtpClient? client = null;
            try
            {
                foreach (var email in pending)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        client ??= CreateSmtpClient();
                        await SendAsync(client, email);
                        email.Status = OutboxEmailStatus.Sent;
                        email.SentAt = DateTime.UtcNow;
                        email.LastError = null;
                    }
                    catch (Exception ex)
                    {
                        email.Attempts++;
                        email.LastError = ex.Message;
                        if (email.Attempts >= MaxAttempts)
                        {
                            email.Status = OutboxEmailStatus.Failed;
                            _logger.LogError(ex, "Email {Id} en échec définitif après {Attempts} tentatives ({To}).", email.Id, email.Attempts, email.ToEmail);
                        }
                        else
                        {
                            // Backoff exponentiel : 30s, 60s, 120s, 240s...
                            var delaySeconds = 30 * Math.Pow(2, email.Attempts - 1);
                            email.NextAttemptAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                            _logger.LogWarning("Échec d'envoi de l'email {Id} (tentative {Attempts}), nouvelle tentative dans {Delay}s.", email.Id, email.Attempts, delaySeconds);
                        }
                    }
                }
            }
            finally
            {
                client?.Dispose();
            }

            await db.SaveChangesAsync(ct);
        }

        private static SmtpClient CreateSmtpClient()
        {
            var host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtpbv.univ-lyon1.fr";
            var portStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
            if (!int.TryParse(portStr, out var port)) port = 587;

            return new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(
                    Environment.GetEnvironmentVariable("SMTP_USERNAME"),
                    Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                )
            };
        }

        private static async Task SendAsync(SmtpClient client, OutboxEmail email)
        {
            var fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")
                ?? throw new InvalidOperationException("SMTP_FROM_EMAIL non défini.");

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = email.Subject,
                Body = email.Body,
                IsBodyHtml = email.IsHtml,
            };
            message.To.Add(email.ToEmail);
            message.Headers.Add("X-Priority", "1");

            await client.SendMailAsync(message);
        }
    }
}
