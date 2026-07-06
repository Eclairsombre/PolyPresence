using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;
using System.Text;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ImportController> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ImportController(ApplicationDbContext context, ILogger<ImportController> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public class ImportIcsModel
        {
            public string IcsUrl { get; set; } = string.Empty;
            public string Year { get; set; } = string.Empty;
            public int? SpecializationId { get; set; }
        }

        /**
         * ImportIcs
         * Point d'entrée pour l'import manuel via API.
         */
        [HttpPost("import-ics")]
        public async Task<IActionResult> ImportIcs([FromBody] ImportIcsModel model)
        {
            if (string.IsNullOrWhiteSpace(model.IcsUrl) || string.IsNullOrWhiteSpace(model.Year))
                return BadRequest(new { error = true, message = "URL ICS ou année manquante." });

            int specializationId;
            if (model.SpecializationId.HasValue)
            {
                var spec = await _context.Specializations.FindAsync(model.SpecializationId.Value);
                if (spec == null)
                    return BadRequest(new { error = true, message = "Filière introuvable." });
                specializationId = spec.Id;
            }
            else
            {
                var defaultSpec = await _context.Specializations.FirstOrDefaultAsync(s => s.Code == "INFO");
                if (defaultSpec == null)
                    return BadRequest(new { error = true, message = "Aucune filière par défaut trouvée." });
                specializationId = defaultSpec.Id;
            }

            try
            {
                _logger.LogInformation($"=== DÉBUT ImportIcs pour l'année {model.Year} ===");

                var rawSessions = await FetchAndParseIcs(model.IcsUrl, model.Year);
                _logger.LogInformation($"{rawSessions.Count} événements trouvés dans l'ICS.");

                var processedSessions = IcsImportHelper.ApplyBusinessRules(rawSessions);
                _logger.LogInformation($"{processedSessions.Count} sessions après application des règles métier.");

                await SyncWithDatabase(processedSessions, model.Year, specializationId);

                _logger.LogInformation($"=== FIN ImportIcs pour {model.Year} ===");
                return Ok(new { success = true, message = "Import terminé avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique lors de l'import ICS.");
                return StatusCode(500, new { error = true, message = ex.Message });
            }
        }

        /**
         * ImportAllIcsLinks
         * Point d'entrée pour l'import automatique (TimerService).
         */
        public async Task ImportAllIcsLinks(ApplicationDbContext context, ILogger logger)
        {
            var links = await context.IcsLinks.ToListAsync();
            foreach (var link in links)
            {
                try
                {
                    logger.LogInformation($"Traitement du lien pour l'année {link.Year}...");
                    var rawSessions = await FetchAndParseIcs(link.Url, link.Year);
                    var processedSessions = IcsImportHelper.ApplyBusinessRules(rawSessions);
                    await SyncWithDatabase(processedSessions, link.Year, link.SpecializationId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Erreur lors de l'import automatique pour {link.Year}");
                }
            }
        }

        private async Task<List<ImportedSession>> FetchAndParseIcs(string url, string year)
        {
            using var client = new HttpClient();
            // Décodage UTF-8 explicite : les exports ADE sont en UTF-8, mais tous les
            // serveurs ne renseignent pas le charset (sinon les accents deviennent illisibles).
            var bytes = await client.GetByteArrayAsync(url);
            var content = Encoding.UTF8.GetString(bytes).TrimStart('﻿');

            var sessions = new List<ImportedSession>();

            foreach (var ev in IcsImportHelper.ParseCalendar(content))
            {
                var professor1 = ev.Professors.Count > 0
                    ? await ResolveOrCreateProfessor(ev.Professors[0].Name, ev.Professors[0].Firstname) : null;
                var professor2 = ev.Professors.Count > 1
                    ? await ResolveOrCreateProfessor(ev.Professors[1].Name, ev.Professors[1].Firstname) : null;

                sessions.Add(new ImportedSession
                {
                    Date = ev.Date,
                    Start = ev.Start,
                    End = ev.End,
                    Name = ev.Name,
                    Room = ev.Room,
                    ProfId = professor1 != null ? professor1.Id.ToString() : "",
                    ProfId2 = professor2 != null ? professor2.Id.ToString() : "",
                    Year = year,
                    TargetGroup = ev.TargetGroup
                });
            }

            return sessions;
        }

        /// <summary>
        /// Retrouve un professeur par (nom, prénom) ou le crée s'il n'existe pas encore.
        /// L'appariement est insensible à la casse pour éviter les doublons.
        /// </summary>
        private async Task<User?> ResolveOrCreateProfessor(string name, string firstname)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var normalizedName = name.Trim();
            var normalizedFirstname = (firstname ?? "").Trim();

            var professor = await _context.Users.FirstOrDefaultAsync(p =>
                p.IsProfessor &&
                p.Name.ToLower() == normalizedName.ToLower() &&
                p.Firstname.ToLower() == normalizedFirstname.ToLower());

            if (professor == null)
            {
                professor = new User
                {
                    Name = normalizedName,
                    Firstname = normalizedFirstname,
                    Email = "",
                    Year = "PROF",
                    IsProfessor = true,
                    Signature = ""
                };
                _context.Users.Add(professor);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Professeur créé à l'import : {normalizedName} {normalizedFirstname}");
            }

            return professor;
        }

        private async Task SyncWithDatabase(List<ImportedSession> importedSessions, string year, int specializationId)
        {
            var existingSessions = await _context.Sessions
                .Where(s => s.Year == year && s.SpecializationId == specializationId)
                .Include(s => s.Attendances)
                .ToListAsync();

            var sessionsToAdd = new List<Session>();
            var sessionsToUpdate = new List<Session>();

            // Matching par créneau exact (Date, Start, End, Year) : mise à jour si trouvé,
            // création sinon. Les existants non retrouvés dans l'import sont supprimés.
            foreach (var imported in importedSessions)
            {
                var importedStartDateTime = DateTime.SpecifyKind(imported.Date.Date + imported.Start, DateTimeKind.Unspecified);
                var importedEndDateTime = DateTime.SpecifyKind(imported.Date.Date + imported.End, DateTimeKind.Unspecified);
                var match = existingSessions.FirstOrDefault(e =>
                    e.Date == imported.Date.Date &&
                    e.StartTime == importedStartDateTime &&
                    e.EndTime == importedEndDateTime &&
                    e.Year == imported.Year);

                if (match != null)
                {
                    bool changed = false;
                    if (match.Name != imported.Name) { match.Name = imported.Name; changed = true; }
                    if (match.Room != imported.Room) { match.Room = imported.Room; changed = true; }
                    if (match.ProfId != imported.ProfId) { match.ProfId = imported.ProfId; changed = true; }
                    if (match.ProfId2 != imported.ProfId2) { match.ProfId2 = imported.ProfId2; changed = true; }
                    if (match.IsMerged != imported.IsMerged) { match.IsMerged = imported.IsMerged; changed = true; }
                    if (match.TargetGroup != imported.TargetGroup) { match.TargetGroup = imported.TargetGroup; changed = true; }

                    if (changed) sessionsToUpdate.Add(match);
                    existingSessions.Remove(match);
                }
                else
                {
                    var newSession = new Session
                    {
                        Date = DateTime.SpecifyKind(imported.Date.Date, DateTimeKind.Unspecified),
                        StartTime = importedStartDateTime,
                        EndTime = importedEndDateTime,
                        Year = year,
                        Name = imported.Name,
                        Room = imported.Room,
                        ProfId = imported.ProfId,
                        ProfId2 = imported.ProfId2,
                        IsMerged = imported.IsMerged,
                        TargetGroup = imported.TargetGroup,
                        ValidationCode = new Random().Next(1000, 9999).ToString(),
                        ProfSignatureToken = Guid.NewGuid().ToString(),
                        ProfSignatureToken2 = !string.IsNullOrEmpty(imported.ProfId2) ? Guid.NewGuid().ToString() : null,
                        SpecializationId = specializationId
                    };
                    sessionsToAdd.Add(newSession);
                }
            }

            // Les sessions restantes n'ont pas été retrouvées dans l'import → suppression.
            var sessionsToDelete = existingSessions;
            if (sessionsToDelete.Any())
                _context.Sessions.RemoveRange(sessionsToDelete);

            if (sessionsToAdd.Any())
                _context.Sessions.AddRange(sessionsToAdd);

            await _context.SaveChangesAsync();

            if (sessionsToAdd.Any())
                await CreateAttendancesForNewSessions(sessionsToAdd, year, specializationId);
        }

        private async Task CreateAttendancesForNewSessions(List<Session> sessions, string year, int specializationId)
        {
            var students = await _context.Users
                .Where(u => u.Year == year && !u.IsDeleted && u.SpecializationId == specializationId)
                .ToListAsync();
            var attendances = new List<Attendance>();

            foreach (var session in sessions)
            {
                foreach (var student in students)
                {
                    attendances.Add(new Attendance
                    {
                        SessionId = session.Id,
                        StudentId = student.Id,
                        Status = AttendanceStatus.Absent
                    });
                }
            }

            if (attendances.Any())
            {
                _context.Attendances.AddRange(attendances);
                await _context.SaveChangesAsync();
            }
        }
    }
}
