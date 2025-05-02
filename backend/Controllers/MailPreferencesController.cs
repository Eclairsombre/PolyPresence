using System.Timers;
using QuestPDF.Fluent;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using backend.Data;
using backend.Models;
using System.IO.Compression;
using System.Globalization;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MailPreferencesController : ControllerBase
    {
        private readonly ILogger<SessionController> _logger;

        private readonly ApplicationDbContext _context;

        private static System.Timers.Timer? _dailyMailTimer;

        private static DateTime _nextExecutionTime;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public MailPreferencesController(ApplicationDbContext context, ILogger<SessionController> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _context = context;
            _serviceScopeFactory = serviceScopeFactory;

            // Configurer une tâche planifiée pour 22h15
            if (_dailyMailTimer == null)
            {
                _nextExecutionTime = GetNextExecutionTime();
                _dailyMailTimer = new System.Timers.Timer((_nextExecutionTime - DateTime.Now).TotalMilliseconds);
                _dailyMailTimer.Elapsed += async (sender, e) => await SendDailyAttendanceSheets();
                _dailyMailTimer.AutoReset = false;
                _dailyMailTimer.Start();
            }
        }

        private DateTime GetNextExecutionTime()
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, 19, now.Minute + 1, 0);
            if (now > target) target = target.AddDays(1);
            return target;
        }

        private async Task SendDailyAttendanceSheets()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionController>>();
                var controller = new MailPreferencesController(scopedContext, logger, _serviceScopeFactory);
                await controller.GenerateAndSendZip();
            }
        }

        // Générer le PDF d'une session
        private byte[] GenerateSessionPdf(Session session, List<(User User, int Status)> attendances)
        {
            using var ms = new MemoryStream();
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Text("Liste de présence")
                    .SemiBold().FontSize(20).AlignCenter();

                    page.Content().Column(column =>
                    {
                        var dateStr = session.Date.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));
                        var horaires = $"{session.StartTime:hh\\:mm} - {session.EndTime:hh\\:mm}";

                        column.Item().Text($"{dateStr} - {session.Year}").FontSize(14).Bold();
                        column.Item().Text($"Horaires : {horaires}").FontSize(12);

                        column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("N°");
                            header.Cell().Element(CellStyle).Text("Nom");
                            header.Cell().Element(CellStyle).Text("Prénom");
                            header.Cell().Element(CellStyle).Text("Présent/Absent");
                            header.Cell().Element(CellStyle).Text("Signature");
                        });

                        int idx = 1;
                        foreach (var (student, status) in attendances)
                        {
                            table.Cell().Element(CellStyle).Text(idx.ToString());
                            table.Cell().Element(CellStyle).Text(student.Name);
                            table.Cell().Element(CellStyle).Text(student.Firstname);
                            table.Cell().Element(CellStyle).Text(status == 0 ? "Présent" : "Absent");

                            if (status == 0 && !string.IsNullOrEmpty(student.Signature))
                            {
                                // Décoder le base64 (data URI) en bytes
                                string base64 = student.Signature;
                                if (base64.StartsWith("data:image"))
                                {
                                    var base64Data = base64.Substring(base64.IndexOf(",") + 1);
                                    try
                                    {
                                        byte[] imageBytes = Convert.FromBase64String(base64Data);
                                        table.Cell().Element(CellStyle).Image(imageBytes);
                                    }
                                    catch
                                    {
                                        table.Cell().Element(CellStyle).Text("Erreur image");
                                    }
                                }
                                else
                                {
                                    table.Cell().Element(CellStyle).Text("Format inconnu");
                                }
                            }
                            else
                            {
                                table.Cell().Element(CellStyle).Text(string.Empty);
                            }
                            idx++;
                        }
                    });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generated on ").FontSize(10);
                        x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(10);
                    });
                });
            });

            document.GeneratePdf(ms); // Ensure you are using QuestPDF.Fluent namespace
            return ms.ToArray();

            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
            {
                return container
                    .PaddingVertical(5)
                    .PaddingHorizontal(5)
                    .Border(1)
                    .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                    .AlignMiddle();
            }
        }

        // Générer et envoyer le ZIP par mail (appelé par le timer)
        public async Task GenerateAndSendZip()
        {
            _logger.LogInformation("Début de la génération et de l'envoi du ZIP des feuilles de présence.");

            var usersWithPrefs = _context.Users
                .Include(u => u.MailPreferences)
                .Where(u => u.MailPreferences != null && u.MailPreferences.Active)
                .ToList();

            _logger.LogInformation($"Nombre d'utilisateurs avec préférences de mail : {usersWithPrefs.Count}");

            foreach (var user in usersWithPrefs)
            {
                var prefs = user.MailPreferences;
                var today = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(DateTime.Now.ToString("dddd", new CultureInfo("fr-FR")));
                _logger.LogInformation($"Préférences de mail pour l'utilisateur {user.StudentNumber} : {prefs.EmailTo}, jours : {string.Join(", ", prefs.Days)}");
                _logger.LogInformation($"Jour d'aujourd'hui : {today}");
                if (!prefs.Days.Contains(today)) continue;
                _logger.LogInformation($"L'utilisateur {user.StudentNumber} a choisi de recevoir le mail aujourd'hui.");

                var sessions = _context.Sessions
                    .Where(s => !_context.SessionSentToUsers.Any(ssu => ssu.SessionId == s.Id && ssu.UserId == user.Id))
                    .ToList();

                if (!sessions.Any()) continue;

                using var zipStream = new MemoryStream();
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var session in sessions)
                    {
                        var attendances = await _context.Attendances
                            .Where(a => a.SessionId == session.Id)
                            .Include(a => a.User)
                            .Select(a => new { a.User, a.Status })
                            .ToListAsync();

                        var attendanceList = attendances
                            .Select(a => ((User)a.User, (int)a.Status))
                            .ToList();

                        var pdfBytes = GenerateSessionPdf(session, attendanceList);

                        var folderPath = $"{session.Date:yyyy}/{session.Date:MM}/{session.Date:dd}";
                        var entry = archive.CreateEntry($"{folderPath}/session_{session.Year}_{session.Date:yyyy-MM-dd}_{session.StartTime:hh\\-mm}.pdf");
                        using var entryStream = entry.Open();
                        entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                    }
                }
                zipStream.Position = 0;
                _logger.LogInformation($"ZIP créé pour l'utilisateur {user.StudentNumber} ({prefs.EmailTo})");
                try
                {
                    var smtpClient = new SmtpClient("smtpbv.univ-lyon1.fr", 587)
                    {
                        EnableSsl = true,
                        Credentials = new NetworkCredential(
                            Environment.GetEnvironmentVariable("SMTP_USERNAME"),
                            Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                        )
                    };

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? throw new InvalidOperationException("SMTP_FROM_EMAIL environment variable is not set")),
                        Subject = "Feuilles de présence quotidiennes",
                        Body = "Veuillez trouver ci-joint les feuilles de présence générées automatiquement.",
                        IsBodyHtml = false
                    };

                    mailMessage.To.Add(prefs.EmailTo);

                    var attachment = new Attachment(zipStream, $"sessions_{DateTime.Now:yyyy-MM-dd}.zip", "application/zip");
                    mailMessage.Attachments.Add(attachment);

                    await smtpClient.SendMailAsync(mailMessage);

                    _logger.LogInformation($"Mail envoyé à {prefs.EmailTo} avec le ZIP des feuilles de présence.");

                    foreach (var session in sessions)
                    {
                        _context.SessionSentToUsers.Add(new SessionSentToUser
                        {
                            SessionId = session.Id,
                            UserId = user.Id,
                            SentAt = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erreur lors de l'envoi de l'email à {prefs.EmailTo} : {ex.Message}");
                }
            }
        }

        [HttpGet("{userId}")]
        public IActionResult GetPreferences(string userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == userId);
            if (user == null) return NotFound("Utilisateur non trouvé.");

            _logger.LogInformation($"Récupération des préférences de mail pour l'utilisateur {userId}");
            _logger.LogInformation($"Préférences de mail ID : {user.MailPreferencesId}");
            _logger.LogInformation($"Email : {user.Email}");

            MailPreferences? preferences;
            if (user.MailPreferencesId == null)
            {
                // Créer un nouvel objet MailPreferences si aucun n'existe
                preferences = new MailPreferences
                {
                    EmailTo = user.Email,
                    Days = new List<string>(),
                    Active = false
                };
                _context.MailPreferences.Add(preferences);
                user.MailPreferencesId = preferences.Id;
                _context.SaveChanges();
            }
            else
            {
                // Récupérer les préférences existantes
                preferences = _context.MailPreferences.FirstOrDefault(mp => mp.Id == user.MailPreferencesId);
                if (preferences == null)
                {
                    return NotFound("Préférences de mail non trouvées.");
                }
            }

            return Ok(preferences);
        }

        [HttpPut("{userId}")]
        public IActionResult UpdatePreferences(string userId, [FromBody] MailPreferences preferences)
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == userId);
            if (user == null) return NotFound("Utilisateur non trouvé.");
            _logger.LogInformation($"Récupération des préférences de mail pour l'utilisateur {userId}");
            _logger.LogInformation($"Préférences de mail ID : {user.MailPreferencesId}");
            _logger.LogInformation($"Email : {user.Email}");

            MailPreferences? existingPreferences;
            if (user.MailPreferencesId == null)
            {
                // Créer un nouvel objet MailPreferences si aucun n'existe
                existingPreferences = new MailPreferences
                {
                    EmailTo = preferences.EmailTo,
                    Days = preferences.Days,
                    Active = preferences.Active
                };
                _context.MailPreferences.Add(existingPreferences);
                user.MailPreferencesId = existingPreferences.Id;
            }
            else
            {
                // Mettre à jour les préférences existantes
                existingPreferences = _context.MailPreferences.FirstOrDefault(mp => mp.Id == user.MailPreferencesId);
                if (existingPreferences == null) return NotFound("Préférences de mail non trouvées.");

                existingPreferences.EmailTo = preferences.EmailTo;
                existingPreferences.Days = preferences.Days;
                existingPreferences.Active = preferences.Active;
            }

            _context.SaveChanges();
            return NoContent();
        }

        [HttpPost("test/{mail}")]
        public IActionResult TestMail(string mail)
        {
            if (string.IsNullOrEmpty(mail))
                return BadRequest("L'adresse email est requise pour tester l'envoi.");

            try
            {
                var smtpClient = new SmtpClient("smtpbv.univ-lyon1.fr", 587)
                {
                    EnableSsl = true, // STARTTLS
                    Credentials = new NetworkCredential(
                        Environment.GetEnvironmentVariable("SMTP_USERNAME"),
                        Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    )
                };

                _logger.LogInformation($"Envoi d'un mail de test de {Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")}");

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? throw new InvalidOperationException("SMTP_FROM_EMAIL environment variable is not set")),
                    Subject = "Test de mail - PolyPresence",
                    Body = "Ceci est un mail de test envoyé depuis PolyPresence via le relais Lyon 1.",
                    IsBodyHtml = false
                };
                mailMessage.To.Add(mail);

                // Envoi
                smtpClient.Send(mailMessage);

                _logger.LogInformation($"Mail de test envoyé à {mail}");
                return Ok("Mail de test envoyé avec succès.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'envoi du mail de test : {ex.Message}");
                return StatusCode(500, "Une erreur est survenue lors de l'envoi du mail.");
            }
        }


        [HttpGet("timer")]
        public IActionResult GetTimer()
        {
            var remainingTime = _nextExecutionTime - DateTime.Now;
            return Ok(new { nextExecutionTime = _nextExecutionTime, remainingTime = remainingTime.ToString(@"hh\:mm\:ss") });
        }
    }
}
