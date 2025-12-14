using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using System.Text.RegularExpressions;

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

            try
            {
                _logger.LogInformation($"=== DÉBUT ImportIcs pour l'année {model.Year} ===");

                // Parsing
                var rawSessions = await FetchAndParseIcs(model.IcsUrl, model.Year);
                _logger.LogInformation($"{rawSessions.Count} événements trouvés dans l'ICS.");

                // Merging
                var processedSessions = ApplyBusinessRules(rawSessions);
                _logger.LogInformation($"{processedSessions.Count} sessions après application des règles métier.");

                // Save
                await SyncWithDatabase(processedSessions, model.Year);

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
                    var processedSessions = ApplyBusinessRules(rawSessions);
                    await SyncWithDatabase(processedSessions, link.Year);
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
            var content = await client.GetStringAsync(url);

            var calendar = Calendar.Load(content);
            var sessions = new List<ImportedSession>();

            var frenchTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris")
                           ?? TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

            foreach (var component in calendar.Events)
            {
                if (component.Start == null || component.End == null) continue;

                // Conversion explicite en DateTime local (France)
                var start = component.Start.AsDateTimeOffset.ToOffset(frenchTz.GetUtcOffset(component.Start.AsDateTimeOffset.DateTime)).DateTime;
                var end = component.End.AsDateTimeOffset.ToOffset(frenchTz.GetUtcOffset(component.End.AsDateTimeOffset.DateTime)).DateTime;

                // Extraction des professeurs depuis la description
                var (p1, f1, p2, f2) = ExtractProfessors(component.Description);

                sessions.Add(new ImportedSession
                {
                    Date = start.Date,
                    Start = start.TimeOfDay,
                    End = end.TimeOfDay,
                    Name = component.Summary ?? "Sans titre",
                    Room = component.Location ?? "",
                    ProfName = p1,
                    ProfFirstname = f1,
                    ProfName2 = p2,
                    ProfFirstname2 = f2,
                    Year = year
                });
            }

            return sessions;
        }

        private (string p1, string f1, string p2, string f2) ExtractProfessors(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return ("", "", "", "");

            // Filtrage des lignes inutiles (Timestamp, Promotion, Export date)
            var lines = description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Where(l => !Regex.IsMatch(l, @"^\d+$")) // Timestamp
                .Where(l => !l.StartsWith("Ingénieur", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith("Diplôme", StringComparison.OrdinalIgnoreCase))
                .Where(l => !l.StartsWith("(Exporté le", StringComparison.OrdinalIgnoreCase))
                .ToList();

            string p1 = "", f1 = "", p2 = "", f2 = "";

            if (lines.Count > 0) ParseName(lines[0], out p1, out f1);
            if (lines.Count > 1) ParseName(lines[1], out p2, out f2);

            return (p1, f1, p2, f2);
        }

        private void ParseName(string fullName, out string name, out string firstname)
        {
            var parts = fullName.Split(' ', 2);
            name = parts.Length > 0 ? parts[0] : "";
            firstname = parts.Length > 1 ? parts[1] : "";
        }

        private List<ImportedSession> ApplyBusinessRules(List<ImportedSession> input)
        {
            // On groupe par date pour traiter jour par jour
            var byDate = input.GroupBy(s => s.Date).ToList();
            var result = new List<ImportedSession>();

            foreach (var dayGroup in byDate)
            {
                var daySessions = dayGroup.ToList();

                // Fusion des chevauchements (Priorité 1)
                // Même horaire (total ou partiel), même promotion, profs différents, intitulés différents
                daySessions = MergeOverlappingSessions(daySessions);

                // Fusion des sessions consécutives (Priorité 2)
                // Même prof, même salle, même intitulé, 15 min d'écart
                daySessions = MergeConsecutiveSessions(daySessions);

                result.AddRange(daySessions);
            }

            return result;
        }

        private List<ImportedSession> MergeOverlappingSessions(List<ImportedSession> sessions)
        {
            // Tri par heure de début
            sessions = sessions.OrderBy(s => s.Start).ToList();
            var merged = new List<ImportedSession>();

            while (sessions.Count > 0)
            {
                var current = sessions[0];
                sessions.RemoveAt(0);

                // On vérifie les chevauchements avec les sessions suivantes
                for (int i = 0; i < sessions.Count; i++)
                {
                    var other = sessions[i];

                    // Chevauchement : Start1 < End2 ET Start2 < End1
                    if (current.Start < other.End && other.Start < current.End)
                    {
                        // Fusion des informations
                        current.Name = CombineStrings(current.Name, other.Name);
                        current.Room = CombineStrings(current.Room, other.Room);

                        // Fusion des profs (déduplication)
                        var allProfs = new List<(string N, string F)>
                        {
                            (current.ProfName, current.ProfFirstname),
                            (current.ProfName2, current.ProfFirstname2),
                            (other.ProfName, other.ProfFirstname),
                            (other.ProfName2, other.ProfFirstname2)
                        }.Where(p => !string.IsNullOrEmpty(p.N)).Distinct().ToList();

                        current.ProfName = allProfs.Count > 0 ? allProfs[0].N : "";
                        current.ProfFirstname = allProfs.Count > 0 ? allProfs[0].F : "";
                        current.ProfName2 = allProfs.Count > 1 ? allProfs[1].N : "";
                        current.ProfFirstname2 = allProfs.Count > 1 ? allProfs[1].F : "";

                        // Extension de la plage horaire
                        if (other.Start < current.Start) current.Start = other.Start;
                        if (other.End > current.End) current.End = other.End;

                        current.IsMerged = true;

                        // On retire la session fusionnée de la liste à traiter
                        sessions.RemoveAt(i);
                        i--;
                    }
                }
                merged.Add(current);
            }

            return merged;
        }

        private List<ImportedSession> MergeConsecutiveSessions(List<ImportedSession> sessions)
        {
            sessions = sessions.OrderBy(s => s.Start).ToList();
            var result = new List<ImportedSession>();

            if (sessions.Count == 0) return result;

            var current = sessions[0];

            for (int i = 1; i < sessions.Count; i++)
            {
                var next = sessions[i];

                // Critères : Même prof, même salle, même nom, écart de 15 min exactement
                bool sameProf = current.ProfName == next.ProfName && current.ProfFirstname == next.ProfFirstname &&
                                current.ProfName2 == next.ProfName2 && current.ProfFirstname2 == next.ProfFirstname2;
                bool sameRoom = current.Room == next.Room;
                bool sameName = current.Name == next.Name;

                bool consecutive = next.Start == current.End.Add(TimeSpan.FromMinutes(15));

                if (sameProf && sameRoom && sameName && consecutive)
                {
                    // Fusion : on étend la fin de la session courante
                    current.End = next.End;
                    current.IsMerged = true;
                }
                else
                {
                    result.Add(current);
                    current = next;
                }
            }
            result.Add(current);

            return result;
        }

        private string CombineStrings(string s1, string s2)
        {
            if (s1 == s2) return s1;
            if (string.IsNullOrEmpty(s1)) return s2;
            if (string.IsNullOrEmpty(s2)) return s1;

            var parts = new List<string>();
            parts.AddRange(s1.Split(" / "));
            parts.AddRange(s2.Split(" / "));
            return string.Join(" / ", parts.Distinct());
        }

        private async Task SyncWithDatabase(List<ImportedSession> importedSessions, string year)
        {
            // Récupération des sessions existantes pour l'année
            var existingSessions = await _context.Sessions
                .Where(s => s.Year == year)
                .Include(s => s.Attendances)
                .ToListAsync();

            var sessionsToAdd = new List<Session>();
            var sessionsToUpdate = new List<Session>();
            var sessionsToDelete = new List<Session>();

            // Matching par clé unique (Date, Start, End, Year)
            // Si une session importée correspond à une session existante (même créneau), on met à jour.
            // Sinon on crée.
            // Les sessions existantes non matchées sont supprimées.

            foreach (var imported in importedSessions)
            {
                var match = existingSessions.FirstOrDefault(e =>
                    e.Date == imported.Date &&
                    e.StartTime == imported.Start &&
                    e.EndTime == imported.End &&
                    e.Year == imported.Year);

                if (match != null)
                {
                    // Update
                    bool changed = false;
                    if (match.Name != imported.Name) { match.Name = imported.Name; changed = true; }
                    if (match.Room != imported.Room) { match.Room = imported.Room; changed = true; }
                    if (match.ProfName != imported.ProfName) { match.ProfName = imported.ProfName; changed = true; }
                    if (match.ProfFirstname != imported.ProfFirstname) { match.ProfFirstname = imported.ProfFirstname; changed = true; }
                    if (match.ProfName2 != imported.ProfName2) { match.ProfName2 = imported.ProfName2; changed = true; }
                    if (match.ProfFirstname2 != imported.ProfFirstname2) { match.ProfFirstname2 = imported.ProfFirstname2; changed = true; }
                    if (match.IsMerged != imported.IsMerged) { match.IsMerged = imported.IsMerged; changed = true; }

                    if (changed) sessionsToUpdate.Add(match);

                    // On retire de la liste des existants pour ne pas le supprimer
                    existingSessions.Remove(match);
                }
                else
                {
                    // Create
                    var newSession = new Session
                    {
                        Date = imported.Date,
                        StartTime = imported.Start,
                        EndTime = imported.End,
                        Year = year,
                        Name = imported.Name,
                        Room = imported.Room,
                        ProfName = imported.ProfName,
                        ProfFirstname = imported.ProfFirstname,
                        ProfName2 = imported.ProfName2,
                        ProfFirstname2 = imported.ProfFirstname2,
                        IsMerged = imported.IsMerged,
                        ValidationCode = new Random().Next(1000, 9999).ToString(),
                        ProfSignatureToken = Guid.NewGuid().ToString(),
                        ProfSignatureToken2 = !string.IsNullOrEmpty(imported.ProfName2) ? Guid.NewGuid().ToString() : null
                    };
                    sessionsToAdd.Add(newSession);
                }
            }

            // Les sessions restantes dans existingSessions n'ont pas été trouvées dans l'import -> Suppression
            sessionsToDelete = existingSessions;

            if (sessionsToDelete.Any())
            {
                _context.Sessions.RemoveRange(sessionsToDelete);
            }

            if (sessionsToAdd.Any())
            {
                _context.Sessions.AddRange(sessionsToAdd);
            }

            // Sauvegarde des changements (Updates, Deletes, Inserts)
            await _context.SaveChangesAsync();

            // Création des feuilles de présence pour les nouvelles sessions
            if (sessionsToAdd.Any())
            {
                await CreateAttendancesForNewSessions(sessionsToAdd, year);
            }
        }

        private async Task CreateAttendancesForNewSessions(List<Session> sessions, string year)
        {
            var students = await _context.Users.Where(u => u.Year == year).ToListAsync();
            var attendances = new List<Attendance>();

            foreach (var session in sessions)
            {
                foreach (var student in students)
                {
                    attendances.Add(new Attendance
                    {
                        SessionId = session.Id,
                        StudentId = student.Id,
                        Status = AttendanceStatus.Absent // Par défaut
                    });
                }
            }

            if (attendances.Any())
            {
                _context.Attendances.AddRange(attendances);
                await _context.SaveChangesAsync();
            }
        }

        // DTO interne pour le traitement
        private class ImportedSession
        {
            public DateTime Date { get; set; }
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public string Name { get; set; } = "";
            public string Room { get; set; } = "";
            public string ProfName { get; set; } = "";
            public string ProfFirstname { get; set; } = "";
            public string ProfName2 { get; set; } = "";
            public string ProfFirstname2 { get; set; } = "";
            public string Year { get; set; } = "";
            public bool IsMerged { get; set; }
        }
    }
}
