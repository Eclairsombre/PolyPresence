using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Claims;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ImportController> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;

        // Cache de résolution des professeurs pour la durée de l'import (un prof enseignant
        // 30 séances était re-cherché 30× en base). Clé = "nom|prénom" insensible à la casse.
        private readonly Dictionary<string, User> _professorCache = new();

        public ImportController(ApplicationDbContext context, ILogger<ImportController> logger, IServiceScopeFactory serviceScopeFactory, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _httpClientFactory = httpClientFactory;
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
        [Authorize]
        public async Task<IActionResult> ImportIcs([FromBody] ImportIcsModel model)
        {
            // Réservé aux admins/délégués : l'import écrase/supprime des sessions et
            // télécharge une URL arbitraire (voir la validation anti-SSRF plus bas).
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
                return Unauthorized(new { error = true, message = "Authentification requise." });
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || currentUser.IsDeleted)
                return Unauthorized(new { error = true, message = "Authentification requise." });
            if (!currentUser.IsAdmin && !currentUser.IsDelegate)
                return Forbid();

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

            // Anti-SSRF (juste avant le téléchargement) : n'autoriser que http(s) vers une
            // adresse publique (bloque localhost, plages privées et IP de métadonnées cloud).
            if (!await IsSafePublicUrlAsync(model.IcsUrl))
                return BadRequest(new { error = true, message = "URL ICS non autorisée." });

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

        /// <summary>
        /// Validation anti-SSRF : n'autorise que http/https vers une adresse IP publique.
        /// Bloque loopback, plages privées (RFC1918), link-local (169.254.x, dont l'IP de
        /// métadonnées cloud 169.254.169.254), CGNAT et adresses IPv6 locales.
        /// </summary>
        private static async Task<bool> IsSafePublicUrlAsync(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
            }
            catch
            {
                return false;
            }

            if (addresses.Length == 0)
                return false;

            foreach (var ip in addresses)
            {
                if (IsPrivateOrReserved(ip))
                    return false;
            }
            return true;
        }

        private static bool IsPrivateOrReserved(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
                return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 10) return true;                              // 10.0.0.0/8
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;             // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return true;             // 169.254.0.0/16 (link-local + métadonnées cloud)
                if (b[0] == 127) return true;                            // 127.0.0.0/8
                if (b[0] == 0) return true;                              // 0.0.0.0/8
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true; // 100.64.0.0/10 (CGNAT)
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
                    return true;
                if (ip.IsIPv4MappedToIPv6)
                    return IsPrivateOrReserved(ip.MapToIPv4());
            }
            return false;
        }

        private async Task<List<ImportedSession>> FetchAndParseIcs(string url, string year)
        {
            var client = _httpClientFactory.CreateClient();
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
            var cacheKey = (normalizedName + "|" + normalizedFirstname).ToLowerInvariant();

            if (_professorCache.TryGetValue(cacheKey, out var cached))
                return cached;

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

            _professorCache[cacheKey] = professor;
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
