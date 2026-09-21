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

            /// <summary>Nature des groupes portés par ce calendrier : sous-groupes, LV1 ou LV2.</summary>
            public GroupType Kind { get; set; } = GroupType.Sub;

            /// <summary>Libellé ADE désignant la promotion entière (voir <see cref="IcsLink.PromoLabel"/>).</summary>
            public string? PromoLabel { get; set; }
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

                await SyncWithDatabase(processedSessions, model.Year, specializationId, model.Kind, model.PromoLabel);

                _logger.LogInformation($"=== FIN ImportIcs pour {model.Year} ===");
                return Ok(new { success = true, message = "Import terminé avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique lors de l'import ICS.");
                return StatusCode(500, new { error = true, message = ex.Message });
            }
        }

        public class PreviewIcsModel
        {
            public string IcsUrl { get; set; } = string.Empty;
        }

        public sealed record IcsLabelDto(string Label, int Count, bool Known, GroupType? KnownType);

        /// <summary>
        /// Analyse un lien ICS sans rien écrire en base : combien d'événements, sur quelle
        /// période, et quels libellés de groupe y figurent.
        ///
        /// Deux usages, tous deux critiques à la saisie :
        ///  - choisir le libellé promo dans une liste plutôt que le retaper au caractère près
        ///    ("Diplôme d'Ingénieur POLYTECH 3A (Informatique)", accents compris) ;
        ///  - voir immédiatement qu'un calendrier est VIDE. C'est le cas réel des emplois du
        ///    temps de langues non encore publiés : sans ce retour, l'admin enregistre un lien,
        ///    ne voit aucune séance apparaître et n'a aucun moyen de comprendre pourquoi.
        ///
        /// Le libellé suggéré est le plus fréquent du calendrier : sur les exports réels,
        /// c'est systématiquement le libellé promo (INFO 3A : 185 séances sur 298 ;
        /// MECA 3A : 165 sur 420, le sous-groupe suivant étant très en dessous).
        /// </summary>
        [HttpPost("preview-ics")]
        [Authorize]
        public async Task<IActionResult> PreviewIcs([FromBody] PreviewIcsModel model)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
                return Unauthorized(new { error = true, message = "Authentification requise." });
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || currentUser.IsDeleted)
                return Unauthorized(new { error = true, message = "Authentification requise." });
            if (!currentUser.IsAdmin && !currentUser.IsDelegate)
                return Forbid();

            if (string.IsNullOrWhiteSpace(model.IcsUrl))
                return BadRequest(new { error = true, message = "URL ICS manquante." });

            if (!await IsSafePublicUrlAsync(model.IcsUrl))
                return BadRequest(new { error = true, message = "URL ICS non autorisée." });

            List<ParsedIcsEvent> events;
            try
            {
                var client = _httpClientFactory.CreateClient();
                var bytes = await client.GetByteArrayAsync(model.IcsUrl);
                var content = Encoding.UTF8.GetString(bytes).TrimStart('\ufeff');
                events = IcsImportHelper.ParseCalendar(content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Analyse du lien ICS impossible.");
                return BadRequest(new { error = true, message = "Impossible de lire ce calendrier : " + ex.Message });
            }

            var counts = events
                .SelectMany(e => e.GroupLabels)
                .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Label = g.First(), Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var known = await _context.Groups
                .Where(g => counts.Select(c => c.Label).Contains(g.Label))
                .ToDictionaryAsync(g => g.Label, g => g.Type, StringComparer.OrdinalIgnoreCase);

            var labels = counts
                .Select(c => new IcsLabelDto(
                    c.Label, c.Count,
                    known.ContainsKey(c.Label),
                    known.TryGetValue(c.Label, out var t) ? t : (GroupType?)null))
                .ToList();

            return Ok(new
            {
                eventCount = events.Count,
                firstDate = events.Count > 0 ? events.Min(e => e.Date) : (DateTime?)null,
                lastDate = events.Count > 0 ? events.Max(e => e.Date) : (DateTime?)null,
                suggestedPromoLabel = labels.FirstOrDefault()?.Label,
                labels
            });
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
                    await SyncWithDatabase(processedSessions, link.Year, link.SpecializationId, link.Kind, link.PromoLabel);
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
                    Uid = ev.Uid,
                    GroupLabels = ev.GroupLabels
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

        /// <summary>
        /// Résout les libellés ADE en groupes de la base, en créant ceux qui n'existent pas.
        ///
        /// Un libellé inconnu est TOUJOURS créé, jamais rejeté : les conventions de nommage
        /// ADE diffèrent d'une promo à l'autre et changent en cours d'année, et une séance
        /// silencieusement perdue serait bien plus grave qu'un groupe en trop. Un groupe
        /// appartenant à une autre filière (les cours mutualisés font apparaître
        /// "MAT5 A", "GBM5A groupe 07 [Ing]"... dans le calendrier INFO) est inoffensif :
        /// aucun étudiant ne lui étant rattaché, il ne génère aucun émargement.
        ///
        /// Seul le libellé déclaré comme <paramref name="promoLabel"/> est promu au type
        /// <see cref="GroupType.Promo"/> — c'est la seule correspondance que les données ne
        /// permettent pas de deviner.
        /// </summary>
        private async Task<Dictionary<string, Group>> ResolveGroupsAsync(
            IReadOnlyDictionary<string, int> labels, int specializationId, string year,
            GroupType kind, string? promoLabel)
        {
            var byLabel = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
            if (labels.Count == 0) return byLabel;

            var existing = await _context.Groups.ToListAsync();
            foreach (var g in existing)
                byLabel[g.Label] = g;

            var normalizedPromo = (promoLabel ?? "").Trim();
            var now = DateTime.UtcNow;
            var created = 0;

            foreach (var (label, occurrences) in labels)
            {
                var isPromo = normalizedPromo.Length > 0
                              && string.Equals(label, normalizedPromo, StringComparison.OrdinalIgnoreCase);

                if (!byLabel.TryGetValue(label, out var group))
                {
                    group = new Group
                    {
                        Label = label,
                        DisplayName = label,
                        Type = isPromo ? GroupType.Promo : kind,
                        SpecializationId = specializationId,
                        Year = year,
                        IsActive = true
                    };
                    _context.Groups.Add(group);
                    byLabel[label] = group;
                    created++;
                }
                else if (isPromo)
                {
                    // Le libellé promo est ré-affirmé à chaque import : un libellé d'abord
                    // découvert via le calendrier d'une autre filière (cours mutualisé) est
                    // ainsi rattaché à la bonne filière dès que SON lien ICS est importé.
                    group.Type = GroupType.Promo;
                    group.SpecializationId = specializationId;
                    group.Year = year;
                }

                // Compteur du DERNIER import, pas un cumul : l'admin veut savoir combien de
                // séances ce libellé pilote aujourd'hui, pour repérer ceux tombés à zéro.
                group.SeenCount = occurrences;
                group.LastSeenAt = now;
            }

            if (created > 0)
                _logger.LogInformation($"{created} groupe(s) découvert(s) et créé(s) à l'import.");

            await _context.SaveChangesAsync();
            return byLabel;
        }

        private async Task SyncWithDatabase(
            List<ImportedSession> importedSessions, string year, int specializationId,
            GroupType kind = GroupType.Sub, string? promoLabel = null)
        {
            var labels = importedSessions
                .SelectMany(s => s.GroupLabels)
                .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var groupsByLabel = await ResolveGroupsAsync(labels, specializationId, year, kind, promoLabel);

            var existingSessions = await _context.Sessions
                .Where(s => s.Year == year && s.SpecializationId == specializationId && s.IcsKind == kind)
                .Include(s => s.Attendances)
                .Include(s => s.SessionGroups)
                .ToListAsync();

            // Rapprochement prioritaire par UID ADE : stable même si le créneau est déplacé,
            // ce qui préserve les présences déjà saisies. Le rapprochement par créneau reste
            // en repli pour les séances créées avant l'introduction de l'UID.
            var byUid = new Dictionary<string, Session>(StringComparer.Ordinal);
            foreach (var s in existingSessions.Where(s => !string.IsNullOrEmpty(s.IcsUid)))
                byUid.TryAdd(s.IcsUid!, s);

            var unmatched = new List<Session>(existingSessions);
            var sessionsToAdd = new List<(Session Session, ImportedSession Imported)>();
            var groupsChanged = new List<Session>();

            foreach (var imported in importedSessions)
            {
                var startDateTime = DateTime.SpecifyKind(imported.Date.Date + imported.Start, DateTimeKind.Unspecified);
                var endDateTime = DateTime.SpecifyKind(imported.Date.Date + imported.End, DateTimeKind.Unspecified);

                Session? match = null;
                if (!string.IsNullOrEmpty(imported.Uid) && byUid.TryGetValue(imported.Uid, out var byUidMatch))
                {
                    match = byUidMatch;
                }
                else
                {
                    match = unmatched.FirstOrDefault(e =>
                        string.IsNullOrEmpty(e.IcsUid) &&
                        e.Date == imported.Date.Date &&
                        e.StartTime == startDateTime &&
                        e.EndTime == endDateTime &&
                        e.Year == imported.Year);
                }

                if (match != null)
                {
                    if (match.Name != imported.Name) match.Name = imported.Name;
                    if (match.Room != imported.Room) match.Room = imported.Room;
                    if (match.ProfId != imported.ProfId) match.ProfId = imported.ProfId;
                    if (match.ProfId2 != imported.ProfId2) match.ProfId2 = imported.ProfId2;
                    if (match.IsMerged != imported.IsMerged) match.IsMerged = imported.IsMerged;
                    if (match.Date != imported.Date.Date) match.Date = imported.Date.Date;
                    if (match.StartTime != startDateTime) match.StartTime = startDateTime;
                    if (match.EndTime != endDateTime) match.EndTime = endDateTime;
                    if (string.IsNullOrEmpty(match.IcsUid) && !string.IsNullOrEmpty(imported.Uid))
                        match.IcsUid = imported.Uid;
                    if (match.IcsKind != kind) match.IcsKind = kind;

                    if (SyncSessionGroups(match, imported, groupsByLabel))
                        groupsChanged.Add(match);

                    unmatched.Remove(match);
                }
                else
                {
                    var newSession = new Session
                    {
                        Date = DateTime.SpecifyKind(imported.Date.Date, DateTimeKind.Unspecified),
                        StartTime = startDateTime,
                        EndTime = endDateTime,
                        Year = year,
                        Name = imported.Name,
                        Room = imported.Room,
                        ProfId = imported.ProfId,
                        ProfId2 = imported.ProfId2,
                        IsMerged = imported.IsMerged,
                        IcsUid = string.IsNullOrEmpty(imported.Uid) ? null : imported.Uid,
                        IcsKind = kind,
                        ValidationCode = new Random().Next(1000, 9999).ToString(),
                        ProfSignatureToken = Guid.NewGuid().ToString(),
                        ProfSignatureToken2 = !string.IsNullOrEmpty(imported.ProfId2) ? Guid.NewGuid().ToString() : null,
                        SpecializationId = specializationId
                    };
                    sessionsToAdd.Add((newSession, imported));
                }
            }

            // Les séances restantes n'ont pas été retrouvées dans l'import → suppression.
            if (unmatched.Any())
                _context.Sessions.RemoveRange(unmatched);

            if (sessionsToAdd.Any())
                _context.Sessions.AddRange(sessionsToAdd.Select(t => t.Session));

            await _context.SaveChangesAsync();

            // Les liaisons des nouvelles séances ne peuvent être écrites qu'une fois leur Id connu.
            foreach (var (session, imported) in sessionsToAdd)
                SyncSessionGroups(session, imported, groupsByLabel);

            await _context.SaveChangesAsync();

            var toRefresh = sessionsToAdd.Select(t => t.Session).Concat(groupsChanged).Distinct().ToList();
            if (toRefresh.Any())
                await SyncAttendancesAsync(toRefresh);
        }

        /// <summary>
        /// Aligne les groupes visés d'une séance sur ceux de l'événement importé.
        /// Renvoie true si l'ensemble a changé (donc si l'émargement doit être recalculé).
        /// </summary>
        private bool SyncSessionGroups(Session session, ImportedSession imported, Dictionary<string, Group> groupsByLabel)
        {
            var wanted = imported.GroupLabels
                .Select(l => groupsByLabel.TryGetValue(l, out var g) ? g : null)
                .Where(g => g != null)
                .Select(g => g!.Id)
                .Distinct()
                .ToHashSet();

            var current = session.SessionGroups.Select(sg => sg.GroupId).ToHashSet();
            if (current.SetEquals(wanted)) return false;

            foreach (var sg in session.SessionGroups.Where(sg => !wanted.Contains(sg.GroupId)).ToList())
            {
                session.SessionGroups.Remove(sg);
                _context.SessionGroups.Remove(sg);
            }

            foreach (var groupId in wanted.Where(id => !current.Contains(id)))
                session.SessionGroups.Add(new SessionGroup { SessionId = session.Id, GroupId = groupId });

            return true;
        }

        /// <summary>
        /// Recalcule les présences des séances dont le public a changé (nouvelles séances, ou
        /// groupes visés modifiés). Les présences en trop ne sont retirées que sur les séances
        /// à venir et encore vierges : on ne détruit jamais un émargement déjà saisi.
        /// </summary>
        private async Task SyncAttendancesAsync(List<Session> sessions)
        {
            if (sessions.Count == 0) return;

            var students = await _context.Users
                .Where(u => !u.IsDeleted && !u.IsProfessor)
                .Select(u => new AudienceStudent(u.Id, u.Year, u.SpecializationId, u.SubGroupId, u.Lv1GroupId, u.Lv2GroupId))
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();

            var groupsBySession = (await _context.SessionGroups
                    .Where(sg => sessionIds.Contains(sg.SessionId))
                    .Select(sg => new { sg.SessionId, sg.Group.Id, sg.Group.Type, sg.Group.SpecializationId, sg.Group.Year })
                    .ToListAsync())
                .GroupBy(x => x.SessionId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<AudienceGroup>)g
                        .Select(x => new AudienceGroup(x.Id, x.Type, x.SpecializationId, x.Year)).ToList());

            var existingAttendances = (await _context.Attendances
                    .Where(a => sessionIds.Contains(a.SessionId))
                    .ToListAsync())
                .GroupBy(a => a.SessionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var today = DateTime.Now.Date;
            var toAdd = new List<Attendance>();
            var toRemove = new List<Attendance>();

            foreach (var session in sessions)
            {
                var target = groupsBySession.TryGetValue(session.Id, out var g) ? g : Array.Empty<AudienceGroup>();
                var audience = SessionAudience.Match(students, target, session.Year, session.SpecializationId)
                    .Select(a => a.Id)
                    .ToHashSet();

                var current = existingAttendances.TryGetValue(session.Id, out var list) ? list : new List<Attendance>();
                var currentIds = current.Select(a => a.StudentId).ToHashSet();

                toAdd.AddRange(audience.Where(id => !currentIds.Contains(id))
                    .Select(id => new Attendance { SessionId = session.Id, StudentId = id, Status = AttendanceStatus.Absent }));

                if (session.Date >= today)
                {
                    toRemove.AddRange(current.Where(a =>
                        !audience.Contains(a.StudentId) &&
                        a.Status == AttendanceStatus.Absent &&
                        string.IsNullOrEmpty(a.Comment)));
                }
            }

            if (toRemove.Any()) _context.Attendances.RemoveRange(toRemove);
            if (toAdd.Any()) _context.Attendances.AddRange(toAdd);

            if (toAdd.Any() || toRemove.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Émargements synchronisés : +{toAdd.Count} / -{toRemove.Count}.");
            }
        }
    }
}
