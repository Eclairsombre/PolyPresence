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
    /*
    * MailPreferencesController
    *
    * This controller handles CRUD operations for MailPreferences entities.
    */
    [ApiController]
    [Route("api/[controller]")]
    public class MailPreferencesController : ControllerBase
    {
        private readonly ILogger<SessionController> _logger;

        private readonly ApplicationDbContext _context;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public MailPreferencesController(ApplicationDbContext context, ILogger<SessionController> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _context = context;
            _serviceScopeFactory = serviceScopeFactory;

        }

        /*
        * GetPdf
        *
        * This method generates a PDF for a specific session and returns it as a file.
        */
        [HttpGet("pdf/{sessionId}")]
        public async Task<IActionResult> GetPdf(int sessionId)
        {
            var session = _context.Sessions
                .Include(s => s.Attendances)
                .ThenInclude(a => a.User)
                .FirstOrDefault(s => s.Id == sessionId);

            if (session == null) return NotFound("Session non trouvée.");

            var attendances = await _context.Attendances
                .Where(a => a.SessionId == session.Id)
                .Include(a => a.User)
                .Select(a => new { a.User, a.Status, a.Comment })
                .ToListAsync();

            var attendanceList = attendances
                .Select(a => ((User)a.User, (int)a.Status, a.Comment))
                .ToList();

            var pdfBytes = GenerateSessionPdf(session, attendanceList);
            var filename = $"session_{session.Year}_{session.Date:yyyy-MM-dd}_{session.StartTime:hh\\-mm}.pdf";

            return File(pdfBytes, "application/pdf", filename);
        }

        /*
        * GenerateSessionPdf
        *
        * This method generates a PDF for a specific session and its attendances.
        */
        private byte[] GenerateSessionPdf(Session session, List<(User User, int Status, string? comment)> attendances)
        {
            // Activer le débogage QuestPDF pour avoir des messages d'erreur plus détaillés
            QuestPDF.Settings.EnableDebugging = true;
            
            using var ms = new MemoryStream();
            string promo = GetPromoYears(session.Year);
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Background("#f6f8fa").Padding(10).Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.ConstantItem(100).Height(70).AlignMiddle().AlignLeft().Element(left =>
                            {
                                string logoRelativePath = Path.Combine("Assets", "polytech_Lyon_logo.png");
                                string logoPath = logoRelativePath;
                                if (!System.IO.File.Exists(logoPath))
                                {
                                    logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "polytech_lyon_logo.png");
                                }
                                if (System.IO.File.Exists(logoPath))
                                {
                                    byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);
                                    left.Image(logoBytes).FitArea();
                                }
                                else
                                {
                                    left.Text("");
                                }
                            });
                            row.RelativeItem().AlignMiddle().AlignCenter().Text("Liste de présence").SemiBold().FontSize(22).FontColor("#2c3e50");
                            row.ConstantItem(100).Height(70).AlignMiddle().AlignRight().Text("");
                        });
                        headerCol.Item().AlignCenter().PaddingTop(4).Text(session.Year + " Promotion " + promo).FontSize(12).FontColor("#34495e");
                    });

                    page.Content().Column(column =>
                    {
                        var dateStr = session.Date.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));
                        var horaires = $"{session.StartTime:hh\\:mm} - {session.EndTime:hh\\:mm}";

                        column.Item().Background("#eaf6fb").BorderLeft(4).BorderColor("#3498db").Padding(12).PaddingLeft(18).Column(schoolCol =>
                        {
                            schoolCol.Item().Text("Etablissement de formation : UCBL1 - EPUL").FontSize(12).FontColor("#2c3e50").Bold();
                            schoolCol.Item().Text("Diplôme : Ingénieur de l'EPUL - spécialité Informatique - apprentissage").FontSize(12).FontColor("#2c3e50").Bold();
                        });

                        column.Item().PaddingTop(2);

                        column.Item().Background("#f8f9fa").Padding(10).Border(1).BorderColor("#e0e4ea").Column(infoCol =>
                        {
                            infoCol.Item().Padding(5).Text($"{dateStr} - {session.Year}").FontSize(14).Bold();
                            if (!string.IsNullOrWhiteSpace(session.Name))
                                infoCol.Item().Padding(5).Text($"Nom de la session : {session.Name}").FontSize(12).Italic();
                            infoCol.Item().Padding(5).Text($"Horaires : {horaires}").FontSize(12);
                            infoCol.Item().Padding(5).Text($"Salle : {session.Room}").FontSize(12);
                            infoCol.Item().Padding(5).Text("");
                            infoCol.Item().Padding(5).Text($"Professeur : {session.ProfFirstname} {session.ProfName} ({session.ProfEmail})").FontSize(12).FontColor("#34495e");
                            if (!string.IsNullOrWhiteSpace(session.ProfSignature))
                            {
                                var base64 = session.ProfSignature;
                                if (base64.StartsWith("data:image"))
                                {
                                    var base64Data = base64.Substring(base64.IndexOf(",") + 1);
                                    try
                                    {
                                        byte[] imageBytes = Convert.FromBase64String(base64Data);
                                        infoCol.Item().Padding(5).Row(row =>
                                        {
                                            row.ConstantItem(80).AlignMiddle().Text("Signature :").FontSize(12);
                                            row.ConstantItem(90).Height(40).AlignMiddle().AlignCenter().Element(container => 
                                                container.Width(90).Height(40).Image(imageBytes).FitArea()
                                            );
                                        });
                                    }
                                    catch
                                    {
                                        infoCol.Item().Padding(5).Text("Signature du professeur : Erreur image");
                                    }
                                }
                                else
                                {
                                    infoCol.Item().Padding(5).Text("Signature du professeur : Format inconnu");
                                }
                            }
                            else
                            {
                                infoCol.Item().Padding(5).Text(text =>
                                {
                                    text.Span("Signature du professeur : ").Italic().FontColor("#888");
                                    text.Span("Non signée");
                                });
                            }
                        });

                        column.Item().PaddingTop(15);

                        column.Item().Table(table =>
                        {
                            var sortedAttendances = attendances.OrderBy(a => a.User.Name).ToList();
                            bool haveComment = sortedAttendances.Any(a => !string.IsNullOrEmpty(a.comment));
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn(2);
                            });


                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).PaddingLeft(2).PaddingRight(2).Text("N°");
                                header.Cell().Element(CellStyle).Padding(5).Text("Nom");
                                header.Cell().Element(CellStyle).Padding(5).Text("Prénom");
                                header.Cell().Element(CellStyle).Padding(5).Text("Présent/Absent");
                                header.Cell().Element(CellStyle).Padding(5).Text("Signature");
                                header.Cell().Element(CellStyle).Padding(5).Text("Retard/Commentaire");
                            });

                            int idx = 1;
                            foreach (var (student, status, comment) in sortedAttendances)
                            {
                                table.Cell().Element(CellStyle).Padding(5).Text(idx.ToString());
                                table.Cell().Element(CellStyle).Padding(5).Text(student.Name);
                                table.Cell().Element(CellStyle).Padding(5).Text(student.Firstname);
                                var isPresent = status == 0;
                                table.Cell().Element(CellStyle).Padding(5).Text(isPresent ? "Présent" : "Absent").FontColor(isPresent ? "#27ae60" : "#c0392b");

                                if (isPresent && !string.IsNullOrEmpty(student.Signature))
                                {
                                    string base64 = student.Signature;
                                    if (base64.StartsWith("data:image"))
                                    {
                                        var base64Data = base64.Substring(base64.IndexOf(",") + 1);
                                        try
                                        {
                                            byte[] imageBytes = Convert.FromBase64String(base64Data);
                                            table.Cell().Element(CellStyle).Padding(5).Element(container => container.Height(30).Image(imageBytes).FitArea());
                                        }
                                        catch
                                        {
                                            table.Cell().Element(CellStyle).Padding(5).Text("Erreur image");
                                        }
                                    }
                                    else
                                    {
                                        table.Cell().Element(CellStyle).Padding(5).Text("Format inconnu");
                                    }
                                }
                                else
                                {
                                    table.Cell().Element(CellStyle).Padding(5).Text(string.Empty);
                                }

                                // Limiter la taille du commentaire pour éviter les dépassements
                                var limitedComment = comment != null && comment.Length > 100 ? comment.Substring(0, 100) + "..." : comment;
                                table.Cell().Element(CellStyle).Padding(5).Text(limitedComment ?? string.Empty).FontSize(10).FontColor("#576574");

                                idx++;
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Généré le ").FontSize(10);
                        x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });
                });
            });

            document.GeneratePdf(ms);
            return ms.ToArray();

            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
            {
                return container
                    .PaddingVertical(5)
                    .PaddingHorizontal(5)
                    .MinHeight(30)
                    .Border(1)
                    .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                    .AlignMiddle();
            }
        }

        /*
        * GenerateAndSendZip
        *
        * This method generates a ZIP file containing PDFs of sessions for users with active mail preferences and sends it via email.
        */
        public async Task GenerateAndSendZip()
        {

            var usersWithPrefs = _context.Users
                .Include(u => u.MailPreferences)
                .Where(u => u.MailPreferences != null && u.MailPreferences.Active)
                .ToList();


            foreach (var user in usersWithPrefs)
            {
                var prefs = user.MailPreferences;
                var today = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(DateTime.Now.ToString("dddd", new CultureInfo("fr-FR")));
                // Si les préférences n'incluent pas le jour actuel, passez à l'utilisateur suivant
                if (prefs?.Days == null || !prefs.Days.Contains(today)) continue;

                var sessions = _context.Sessions
                    .Where(s => !_context.SessionSentToUsers.Any(ssu => ssu.SessionId == s.Id && ssu.UserId == user.Id) &&
                                s.Date <= DateTime.Now)
                    .ToList();

                if (sessions.Count == 0) continue;

                using var zipStream = new MemoryStream();
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var session in sessions)
                    {

                        var attendances = await _context.Attendances
                            .Where(a => a.SessionId == session.Id)
                            .Include(a => a.User)
                            .Select(a => new { a.User, a.Status, a.Comment })
                            .ToListAsync();

                        var attendanceList = attendances
                            .Select(a => ((User)a.User, (int)a.Status, a.Comment))
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
                    var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtpbv.univ-lyon1.fr";
                    var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
                    if (!int.TryParse(smtpPortStr, out var smtpPort)) smtpPort = 587;

                    var smtpClient = new SmtpClient(smtpHost, smtpPort)
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
                    mailMessage.Headers.Add("X-Priority", "1");

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

        /*
        * GetPreferences
        *
        * This method retrieves the mail preferences for a specific user.
        */
        [HttpGet("{userId}")]
        public IActionResult GetPreferences(string userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == userId);
            if (user == null) return NotFound("Utilisateur non trouvé.");

            MailPreferences? preferences;
            if (user.MailPreferencesId == null)
            {
                preferences = new MailPreferences
                {
                    EmailTo = user.Email,
                    Days = new List<string>(),
                    Active = false
                };
                _context.MailPreferences.Add(preferences);
                _context.SaveChanges();
                user.MailPreferencesId = preferences.Id;
                _context.SaveChanges();
            }
            else
            {
                preferences = _context.MailPreferences.FirstOrDefault(mp => mp.Id == user.MailPreferencesId);
                if (preferences == null)
                {
                    return NotFound("Préférences de mail non trouvées.");
                }
            }

            return Ok(preferences);
        }

        /*
        * UpdatePreferences
        *
        * This method updates the mail preferences for a specific user.
        */
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
                existingPreferences = _context.MailPreferences.FirstOrDefault(mp => mp.Id == user.MailPreferencesId);
                if (existingPreferences == null) return NotFound("Préférences de mail non trouvées.");

                existingPreferences.EmailTo = preferences.EmailTo;
                existingPreferences.Days = preferences.Days;
                existingPreferences.Active = preferences.Active;
            }

            _context.SaveChanges();
            return NoContent();
        }

        /*
        * TestMail
        *
        * This method sends a test email to the specified address.
        */
        [HttpPost("test/{mail}")]
        public IActionResult TestMail(string mail)
        {
            if (string.IsNullOrEmpty(mail))
                return BadRequest("L'adresse email est requise pour tester l'envoi.");

            try
            {
                var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtpbv.univ-lyon1.fr";
                var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
                if (!int.TryParse(smtpPortStr, out var smtpPort)) smtpPort = 587;

                var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
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
                mailMessage.Headers.Add("X-Priority", "1");

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
        /*
        * GetPromoYears
        *
        * This method calculates the school years for a given promotion.
        */
        public static string GetPromoYears(string promo)
        {
            var now = DateTime.Now;
            int schoolYearStart = now.Month >= 9 ? now.Year : now.Year - 1;
            int startYear = 0, endYear = 0;

            switch (promo)
            {
                case "3A":
                    startYear = schoolYearStart;
                    endYear = schoolYearStart + 3;
                    break;
                case "4A":
                    startYear = schoolYearStart - 1;
                    endYear = schoolYearStart + 2;
                    break;
                case "5A":
                    startYear = schoolYearStart - 2;
                    endYear = schoolYearStart + 1;
                    break;
                default:
                    return "";
            }
            return $"{startYear}-{endYear}";
        }
    }
}

