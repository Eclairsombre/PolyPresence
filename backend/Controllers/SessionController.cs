using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Ical.Net.CalendarComponents;
using Ical.Net.Proxies;

namespace backend.Controllers
{
    /**
     * SessionController
     *
     * This controller handles CRUD operations for Session entities.
     */
    [Route("api/[controller]")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public SessionController(ApplicationDbContext context, ILogger<SessionController> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _logger = logger;
            _scopeFactory = serviceScopeFactory;
        }



        /**
         * CheckAndSendSessionMails
         *
         * This method checks for sessions that need to send emails to professors and sends them.
         */
        public async Task CheckAndSendSessionMails()
        {
            var now = DateTime.Now;
            _logger.LogInformation($"Vérification des sessions à {now} pour l'envoi de mails aux professeurs.");

            var sessions = await _context.Sessions
                .Where(s => s.Date == now.Date &&
                        ((!s.IsMailSent && s.ProfId != null) ||
                            (!s.IsMailSent2 && s.ProfId2 != null)))
                .ToListAsync();

            sessions = sessions
                .Where(s => s.StartTime <= now)
                .ToList();

            foreach (var session in sessions)
            {
                if (!session.IsMailSent && !string.IsNullOrEmpty(session.ProfId))
                {
                    try
                    {
                        if (await ProfessorUsesAccountMode(session.ProfId))
                        {
                            // Le professeur consulte ses cours depuis son espace : pas de mail.
                            session.IsMailSent = true;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Professeur 1 en mode 'Account' pour la session {session.Id} : aucun mail envoyé.");
                        }
                        else
                        {
                            await SendProfSignatureMail(session, 1);
                            session.IsMailSent = true;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Mail envoyé automatiquement au professeur 1 pour la session {session.Id}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Erreur lors de l'envoi automatique du mail au professeur 1 pour la session {session.Id} : {ex.Message}");
                    }
                }

                if (!session.IsMailSent2 && !string.IsNullOrEmpty(session.ProfId2))
                {
                    try
                    {
                        if (await ProfessorUsesAccountMode(session.ProfId2))
                        {
                            session.IsMailSent2 = true;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Professeur 2 en mode 'Account' pour la session {session.Id} : aucun mail envoyé.");
                        }
                        else
                        {
                            await SendProfSignatureMail(session, 2);
                            session.IsMailSent2 = true;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Mail envoyé automatiquement au professeur 2 pour la session {session.Id}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Erreur lors de l'envoi automatique du mail au professeur 2 pour la session {session.Id} : {ex.Message}");
                    }
                }
            }
        }

        /**
         * GetAllSessions
         *
         * Récupère les sessions avec filtres serveur (année, filière, plage de dates)
         * et pagination optionnelle. Le code de validation n'est inclus que pour les
         * délégués/administrateurs.
         *
         * - Si `page` est fourni : renvoie une enveloppe paginée { items, total, page, pageSize, totalPages }.
         * - Sinon : renvoie le tableau filtré (rétrocompatibilité avec les anciens appels).
         */
        [HttpGet]
        public async Task<ActionResult<object>> GetSessions(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? year = null,
            [FromQuery] int? specializationId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _context.Sessions
                .AsNoTracking()
                .Include(s => s.Specialization)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(year))
                query = query.Where(s => s.Year == year);

            if (specializationId.HasValue)
                query = query.Where(s => s.SpecializationId == specializationId.Value);

            if (from.HasValue)
            {
                var fromDate = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Unspecified);
                query = query.Where(s => s.Date >= fromDate);
            }
            if (to.HasValue)
            {
                var toDate = DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Unspecified);
                query = query.Where(s => s.Date <= toDate);
            }

            // Tri serveur (remplace le tri client).
            query = query.OrderBy(s => s.Date).ThenBy(s => s.StartTime);

            var isAdmin = false;
            var isDelegate = false;
            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                var currentUser = await _context.Users.FindAsync(userId);
                if (currentUser != null)
                {
                    isAdmin = currentUser.IsAdmin;
                    isDelegate = currentUser.IsDelegate;
                }
            }

            var total = await query.CountAsync();

            List<Session> sessions;
            int currentPage = 1;
            int size = total == 0 ? 1 : total;
            if (page.HasValue)
            {
                currentPage = page.Value < 1 ? 1 : page.Value;
                size = pageSize.GetValueOrDefault(20);
                if (size < 1) size = 20;
                if (size > 500) size = 500; // garde-fou
                sessions = await query.Skip((currentPage - 1) * size).Take(size).ToListAsync();
            }
            else
            {
                sessions = await query.ToListAsync();
            }

            object Project(Session s) => isAdmin || isDelegate
                ? new
                {
                    s.Id, s.Date, s.StartTime, s.EndTime, s.Year, s.Name, s.Room,
                    s.ValidationCode, s.ProfId, s.ProfSignature, s.ProfSignatureToken,
                    s.ProfId2, s.ProfSignature2, s.ProfSignatureToken2, s.TargetGroup,
                    s.IsSent, s.IsMailSent, s.IsMailSent2, s.IsMerged, s.SpecializationId,
                    SpecializationName = s.Specialization?.Name,
                    SpecializationCode = s.Specialization?.Code
                }
                : new
                {
                    s.Id, s.Date, s.StartTime, s.EndTime, s.Year, s.Name, s.Room,
                    s.ProfId, s.ProfSignature, s.ProfSignatureToken,
                    s.ProfId2, s.ProfSignature2, s.ProfSignatureToken2,
                    s.IsSent, s.IsMailSent, s.IsMailSent2, s.SpecializationId,
                    SpecializationName = s.Specialization?.Name,
                    SpecializationCode = s.Specialization?.Code
                };

            var items = sessions.Select(Project).ToList();

            if (page.HasValue)
            {
                return Ok(new
                {
                    items,
                    total,
                    page = currentPage,
                    pageSize = size,
                    totalPages = (int)Math.Ceiling(total / (double)size)
                });
            }

            return Ok(items);
        }

        /**
         * GetSession
         *
         * This method retrieves a session by its ID from the database.
         * Le code de validation n'est inclus que si l'utilisateur est un délégué ou un administrateur.
         */
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetSession(int id)
        {
            var session = await _context.Sessions
                .Include(s => s.Specialization)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return NotFound();
            }

            var isAdmin = false;
            var isDelegate = false;

            if (User.Identity?.IsAuthenticated == true)
            {
                var isAdminClaim = User.FindFirstValue("role");
                var isDelegateClaim = User.FindFirstValue("isDelegate");

                isAdmin = isAdminClaim == "Admin";
                isDelegate = isDelegateClaim == "true";

                var userStudentNumber = User.FindFirstValue("studentNumber");
                _logger.LogInformation($"User {userStudentNumber} requesting session {id} - Role: {isAdminClaim}, IsDelegate: {isDelegateClaim}");
                _logger.LogInformation($"Interpreted values - IsAdmin: {isAdmin}, IsDelegate: {isDelegate}");
            }
            else
            {
                _logger.LogInformation($"No authenticated user found for session {id} request");
            }

            if (!isAdmin && !isDelegate)
            {
                _logger.LogInformation($"Hiding validation code in session {id} response");

                return new ActionResult<object>(new
                {
                    session.Id,
                    session.Date,
                    session.StartTime,
                    session.EndTime,
                    session.Year,
                    session.Name,
                    session.Room,
                    session.ProfId,
                    session.ProfSignature,
                    session.ProfSignatureToken,
                    session.ProfId2,
                    session.ProfSignature2,
                    session.ProfSignatureToken2,
                    session.IsSent,
                    session.IsMailSent,
                    session.IsMailSent2,
                    session.SpecializationId,
                    SpecializationName = session.Specialization?.Name,
                    SpecializationCode = session.Specialization?.Code
                });
            }

            _logger.LogInformation($"Returning full session {id} with validation code");
            return new ActionResult<object>(new
            {
                session.Id,
                session.Date,
                session.StartTime,
                session.EndTime,
                session.Year,
                session.Name,
                session.Room,
                session.ValidationCode,
                session.ProfId,
                session.ProfSignature,
                session.ProfSignatureToken,
                session.ProfId2,
                session.ProfSignature2,
                session.ProfSignatureToken2,
                session.TargetGroup,
                session.IsSent,
                session.IsMailSent,
                session.IsMailSent2,
                session.IsMerged,
                session.SpecializationId,
                SpecializationName = session.Specialization?.Name,
                SpecializationCode = session.Specialization?.Code
            });
        }

        /**
         * GetSessionsByYear
         *
         * This method retrieves sessions by year from the database.
         * Le code de validation n'est inclus que si l'utilisateur est un délégué ou un administrateur.
         */
        [HttpGet("year/{year}")]
        public async Task<ActionResult<IEnumerable<object>>> GetSessionsByYear(string year)
        {
            var sessions = await _context.Sessions
                .Where(s => s.Year == year)
                .Include(s => s.Specialization)
                .ToListAsync();

            if (sessions == null || sessions.Count == 0)
            {
                return NotFound(new { message = $"Aucune session trouvée pour l'année {year}" });
            }

            var isAdmin = false;
            var isDelegate = false;

            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                var currentUser = await _context.Users.FindAsync(userId);
                if (currentUser != null)
                {
                    isAdmin = currentUser.IsAdmin;
                    isDelegate = currentUser.IsDelegate;
                }
            }

            if (!isAdmin && !isDelegate)
            {
                var sessionsWithoutCode = sessions.Select(s => new
                {
                    s.Id,
                    s.Date,
                    s.StartTime,
                    s.EndTime,
                    s.Year,
                    s.Name,
                    s.Room,
                    s.ProfId,
                    s.ProfSignature,
                    s.ProfSignatureToken,
                    s.ProfId2,
                    s.ProfSignature2,
                    s.ProfSignatureToken2,
                    s.IsSent,
                    s.IsMailSent,
                    s.IsMailSent2,
                    s.SpecializationId,
                    SpecializationName = s.Specialization?.Name,
                    SpecializationCode = s.Specialization?.Code
                }).ToList();

                return sessionsWithoutCode;
            }

            return sessions.Select(s => new
            {
                s.Id,
                s.Date,
                s.StartTime,
                s.EndTime,
                s.Year,
                s.Name,
                s.Room,
                s.ValidationCode,
                s.ProfId,
                s.ProfSignature,
                s.ProfSignatureToken,
                s.ProfId2,
                s.ProfSignature2,
                s.ProfSignatureToken2,
                s.TargetGroup,
                s.IsSent,
                s.IsMailSent,
                s.IsMailSent2,
                s.IsMerged,
                s.SpecializationId,
                SpecializationName = s.Specialization?.Name,
                SpecializationCode = s.Specialization?.Code
            }).ToList();
        }

        /**
         * GetMyProfessorSessions
         *
         * Renvoie les sessions du jour pour lesquelles le professeur connecté est
         * assigné (slot 1 ou 2), avec le token de signature correspondant et l'état
         * de signature. Sert de "page statique" du cours en cours pour le professeur.
         */
        [HttpGet("my-prof-sessions")]
        [Authorize]
        public async Task<IActionResult> GetMyProfessorSessions()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || currentUser.IsDeleted)
                return NotFound(new { message = "Utilisateur connecté introuvable." });

            if (!currentUser.IsProfessor)
                return Forbid();

            var profId = currentUser.Id.ToString();
            var today = DateTime.Now.Date;
            var now = DateTime.Now;

            var sessions = await _context.Sessions
                .Include(s => s.Specialization)
                .Where(s => s.Date == today && (s.ProfId == profId || s.ProfId2 == profId))
                .ToListAsync();

            var result = sessions
                .Select(s =>
                {
                    var isMainProf = s.ProfId == profId;
                    return new
                    {
                        sessionId = s.Id,
                        date = s.Date,
                        startTime = s.StartTime,
                        endTime = s.EndTime,
                        year = s.Year,
                        name = s.Name,
                        room = s.Room,
                        specializationName = s.Specialization != null ? s.Specialization.Name : null,
                        validationCode = s.ValidationCode,
                        signatureToken = isMainProf ? s.ProfSignatureToken : s.ProfSignatureToken2,
                        isMainProfessor = isMainProf,
                        alreadySigned = isMainProf
                            ? !string.IsNullOrEmpty(s.ProfSignature)
                            : !string.IsNullOrEmpty(s.ProfSignature2),
                        isCurrent = s.StartTime <= now && now <= s.EndTime,
                        isPast = s.EndTime < now
                    };
                })
                .OrderBy(s => s.startTime)
                .ToList();

            return Ok(result);
        }

        /**
         * PostSession
         *
         * This method creates a new session in the database.
         */
        [HttpPost]
        public async Task<ActionResult<Session>> PostSession(Session session)
        {
            _logger.LogInformation("Création d'une nouvelle session");
            session.ProfSignatureToken = Guid.NewGuid().ToString();

            if (!string.IsNullOrEmpty(session.ProfId2))
            {
                session.ProfSignatureToken2 = Guid.NewGuid().ToString();
            }

            // Forcer Kind=Unspecified pour les colonnes PostgreSQL 'timestamp without time zone'
            session.Date = DateTime.SpecifyKind(session.Date, DateTimeKind.Unspecified);
            session.StartTime = DateTime.SpecifyKind(session.StartTime, DateTimeKind.Unspecified);
            session.EndTime = DateTime.SpecifyKind(session.EndTime, DateTimeKind.Unspecified);

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
        }

        /**
         * GetSessionByProfSignatureToken
         *
         * This method retrieves a session by its professor signature token from the database.
         */
        [HttpGet("prof-signature/{token}")]
        public async Task<ActionResult<Session>> GetSessionByProfSignatureToken(string token)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.ProfSignatureToken == token || s.ProfSignatureToken2 == token);
            if (session == null)
            {
                return NotFound(new { error = true, message = "Session non trouvée pour ce lien." });
            }
            return session;
        }

        /**
         * SaveProfSignature
         *
         * This method saves the professor's signature for a session.
         */
        [HttpPost("prof-signature/{token}")]
        public async Task<IActionResult> SaveProfSignature(string token, [FromBody] SignatureModel signatureData)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.ProfSignatureToken == token || s.ProfSignatureToken2 == token);
            if (session == null)
            {
                return NotFound(new { error = true, message = "Session non trouvée pour ce lien." });
            }

            if (session.ProfSignatureToken == token)
            {
                session.ProfSignature = signatureData.Signature;
            }
            else if (session.ProfSignatureToken2 == token)
            {
                session.ProfSignature2 = signatureData.Signature;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Signature enregistrée avec succès." });
        }

        /**
         * PutSession
         *
         * This method updates an existing session in the database.
         * Only administrators or delegates can update sessions.
         */
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutSession(int id, Session session)
        {
            if (id != session.Id)
            {
                return BadRequest();
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            if (!currentUser.IsAdmin && !currentUser.IsDelegate)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé: {currentUser.StudentNumber} a tenté de modifier la session ID {id}");
                return Forbid();
            }
            _context.Entry(session).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Session {id} modifiée avec succès");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SessionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        /**
         * DeleteSession
         *
         * This method deletes a session from the database.
         * Only administrators can delete sessions.
         */
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteSession(int id)
        {
            // Vérifier les autorisations de l'utilisateur
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            // Vérifier si l'utilisateur est administrateur
            if (!currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé: {currentUser.StudentNumber} a tenté de supprimer la session ID {id}");
                return Forbid();
            }

            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            var attendances = _context.Attendances.Where(a => a.SessionId == id);
            _context.Attendances.RemoveRange(attendances);

            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Trouve la session "en cours" pour un étudiant donné, sur la base de son
        /// inscription (présence enregistrée). C'est plus précis qu'un simple filtre
        /// par année : l'inscription encode déjà la filière ET le sous-groupe (TargetGroup),
        /// donc deux filières d'une même promo avec des cours simultanés ne se mélangent pas.
        /// Renvoie null si l'utilisateur n'a aucun cours en cours.
        /// </summary>
        private static async Task<Session?> FindCurrentEnrolledSessionAsync(
            ApplicationDbContext db, int userId, CancellationToken cancellationToken = default)
        {
            if (userId == 0) return null;

            var now = DateTime.Now;
            var today = now.Date;

            // Sessions du jour où l'étudiant est inscrit (a une ligne de présence).
            var sessionsToday = await db.Sessions
                .AsNoTracking()
                .Include(s => s.Specialization)
                .Where(s => s.Date == today &&
                            db.Attendances.Any(a => a.SessionId == s.Id && a.StudentId == userId))
                .ToListAsync(cancellationToken);

            // Filtre horaire en mémoire (cohérent avec le comportement historique).
            return sessionsToday.FirstOrDefault(s => s.StartTime <= now && s.EndTime >= now);
        }

        /**
         * GetCurrentSession
         *
         * Renvoie le "cours actuel" de l'étudiant connecté, déterminé par son inscription
         * (présence) et non par sa seule année — ce qui respecte la filière et le sous-groupe.
         * Le paramètre {year} est conservé pour la compatibilité de route mais n'est plus
         * utilisé pour le filtrage. Le code de validation n'est inclus que pour un délégué/admin.
         */
        [HttpGet("current/{year}")]
        public async Task<ActionResult<object>> GetCurrentSession(string year)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);

            var currentSession = await FindCurrentEnrolledSessionAsync(_context, userId);

            if (currentSession == null)
            {
                return NotFound(new { message = "Aucune session en cours pour l'utilisateur." });
            }

            var isAdmin = false;
            var isDelegate = false;

            if (User.Identity?.IsAuthenticated == true)
            {
                isAdmin = User.FindFirstValue("role") == "Admin";
                isDelegate = User.FindFirstValue("isDelegate") == "true";
            }

            if (!isAdmin && !isDelegate)
            {
                return new ActionResult<object>(new
                {
                    currentSession.Id,
                    currentSession.Date,
                    currentSession.StartTime,
                    currentSession.EndTime,
                    currentSession.Year,
                    currentSession.Name,
                    currentSession.Room,
                    currentSession.IsSent,
                    currentSession.IsMailSent,
                    currentSession.SpecializationId,
                    SpecializationName = currentSession.Specialization?.Name
                });
            }

            return new ActionResult<object>(currentSession);
        }

        /**
         * StreamCurrentSession (SSE)
         *
         * Flux Server-Sent Events qui pousse le "cours actuel" de l'étudiant connecté
         * (session en cours pour son année + son statut de présence) dès qu'il change :
         * un cours commence, se termine, ou son statut bascule (présent/absent/annulé).
         * Utilisé par le tableau de bord étudiant pour une mise à jour automatique sans refresh.
         *
         * Authentification : EventSource ne peut pas envoyer d'en-tête Authorization,
         * on valide donc le JWT passé en query (?access_token=...). Le middleware laisse
         * passer les chemins en "/stream" (ils s'authentifient eux-mêmes).
         */
        [HttpGet("current/{year}/stream")]
        public async Task StreamCurrentSession(string year, [FromQuery] string? access_token, CancellationToken cancellationToken)
        {
            // 1) Authentifier via le token de query avant d'ouvrir le flux SSE.
            int userId;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

                var principal = string.IsNullOrEmpty(access_token)
                    ? null
                    : await jwtService.ValidateTokenAsync(access_token);

                if (principal == null ||
                    !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId))
                {
                    Response.StatusCode = 401;
                    return;
                }

                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user == null || user.IsDeleted)
                {
                    Response.StatusCode = 401;
                    return;
                }
            }

            // 2) Ouvrir le flux SSE.
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no"; // évite le buffering par nginx

            // camelCase pour être cohérent avec le reste de l'API (MVC sérialise en camelCase)
            // et avec ce que lit le front (payload.session.id, payload.status).
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            string? lastJson = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string json;
                    // Scope/DbContext frais à chaque itération : la connexion SSE est longue.
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Cours actuel basé sur l'inscription de l'étudiant (filière + sous-groupe),
                        // pas sur la seule année. Le paramètre {year} reste dans la route uniquement
                        // pour la distinguer de "current/{year}".
                        var current = await FindCurrentEnrolledSessionAsync(db, userId, cancellationToken);

                        object? payload = null;
                        if (current != null)
                        {
                            var attendance = await db.Attendances
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.SessionId == current.Id && a.StudentId == userId, cancellationToken);

                            payload = new
                            {
                                session = new
                                {
                                    current.Id,
                                    current.Date,
                                    current.StartTime,
                                    current.EndTime,
                                    current.Year,
                                    current.Name,
                                    current.Room,
                                    current.SpecializationId,
                                    SpecializationName = current.Specialization != null ? current.Specialization.Name : null
                                },
                                status = attendance != null ? (int?)(int)attendance.Status : null
                            };
                        }

                        json = System.Text.Json.JsonSerializer.Serialize(payload, jsonOptions);
                    }

                    if (json != lastJson)
                    {
                        lastJson = json;
                        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    }
                    else
                    {
                        // Commentaire SSE = heartbeat, garde la connexion ouverte.
                        await Response.WriteAsync(": ping\n\n", cancellationToken);
                    }
                    await Response.Body.FlushAsync(cancellationToken);

                    await Task.Delay(2000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Client déconnecté : fin normale du flux.
            }
        }

        /**
         * AddStudentsToSession
         *
         * This method adds a student to a session.
         */
        [HttpPost("{sessionId}/student/{studentNumber}")]
        public async Task<IActionResult> AddStudentsToSession(int sessionId, string studentNumber)
        {
            _logger.LogDebug($"Tentative d'ajout de l'étudiant {studentNumber} à la session {sessionId}");

            var session = await _context.Sessions.FindAsync(sessionId);

            if (session == null)
            {
                return NotFound(new { error = true, message = $"Session avec l'ID {sessionId} non trouvée." });
            }

            _logger.LogDebug($"Session trouvée : {session.Id} - {session.Year}");
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return NotFound(new { error = true, message = "Aucun utilisateur trouvé avec les identifiants fournis." });
            }

            _logger.LogDebug($"Étudiant trouvé : {user.Id} - {user.StudentNumber}");

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == user.Id);

            if (existingAttendance != null)
            {
                return Conflict(new { error = true, message = $"L'étudiant {studentNumber} est déjà inscrit à cette session." });
            }

            var attendance = new Attendance
            {
                SessionId = sessionId,
                StudentId = user.Id,
                Status = AttendanceStatus.Absent,
            };
            _context.Attendances.Add(attendance);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Étudiant ajouté à la session avec succès." });
        }

        /**
         * ValidateSession
         *
         * This method validates a session for a student using a validation code.
         * Utilisateurs peuvent uniquement valider leur propre présence, sauf si admin.
         */
        [HttpPost("{sessionId}/validate/{studentNumber}")]
        [Authorize]
        public async Task<IActionResult> ValidateSession(int sessionId, string studentNumber, [FromBody] ValidateSessionModel model)
        {
            // Vérification de session existante
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = true, message = $"Session avec l'ID {sessionId} non trouvée." });
            }
            // Vérification si la session a déjà été signée par le professeur
            if ((session.ProfSignature != null && session.ProfSignature != "" && session.ProfId != null) || (session.ProfSignature2 != null && session.ProfSignature2 != "" && session.ProfId2 != null))
            {
                return BadRequest(new { error = true, message = "La session a déjà été signée par le professeur, la validation de présence est fermée." });
            }

            // Vérification du code de validation
            if (model.ValidationCode != session.ValidationCode)
            {
                _logger.LogWarning($"Tentative de validation avec un code incorrect: {model.ValidationCode} pour la session {sessionId}");
                return BadRequest(new { error = true, message = "Le code de validation est incorrect." });
            }

            // Comparer avec l'heure locale (CET) car les dates sont stockées en heure locale
            if (session.Date.Date != DateTime.Now.Date)
            {
                return BadRequest(new { error = true, message = "La validation de présence n'est autorisée que le jour de la session." });
            }

            // Récupération de l'utilisateur cible (celui dont on valide la présence)
            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (targetUser == null)
            {
                return NotFound(new { error = true, message = "Aucun utilisateur trouvé avec les identifiants fournis." });
            }

            // Récupération de l'utilisateur authentifié
            var authenticatedUserNumber = User.FindFirstValue("studentNumber");
            var isAdmin = User.FindFirstValue("role") == "Admin";
            var isDelegate = User.FindFirstValue("isDelegate") == "true";

            // Vérification des droits: l'utilisateur ne peut valider que sa propre présence sauf s'il est admin
            if (!isAdmin && authenticatedUserNumber != studentNumber)
            {
                _logger.LogWarning($"Tentative non autorisée: L'utilisateur {authenticatedUserNumber} essaie de valider la présence de {studentNumber}");
                return Forbid();
            }

            // Recherche de l'enregistrement de présence
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == targetUser.Id);
            if (attendance == null)
            {
                return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session et cet étudiant." });
            }

            if (attendance.Status == AttendanceStatus.Present)
            {
                return Conflict(new { error = true, message = "La présence a déjà été validée pour cette session." });
            }

            // Mise à jour du statut de présence
            attendance.Status = AttendanceStatus.Present;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Présence validée avec succès pour l'étudiant {studentNumber} à la session {sessionId}");
            return Ok(new { message = "Présence validée avec succès." });
        }

        /**
         * ValidateSessionModel
         *
         * This model is used for validating a session with a code.
         */
        public class ValidateSessionModel
        {
            public string ValidationCode { get; set; } = string.Empty;
        }

        /**
         * GetAttendance
         *
         * This method retrieves the attendance for a specific session and student.
         * Le code de validation de la session n'est inclus que si l'utilisateur est un délégué ou un administrateur.
         */
        [HttpGet("{sessionId}/attendance/{studentNumber}")]
        public async Task<IActionResult> GetAttendance(int sessionId, string studentNumber)
        {
            var session = await _context.Sessions.FindAsync(sessionId);

            if (session == null)
            {
                return NotFound(new { error = true, message = $"Session avec l'ID {sessionId} non trouvée." });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return NotFound(new { error = true, message = "Aucun utilisateur trouvé avec les identifiants fournis." });
            }

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == user.Id);

            if (attendance == null)
            {
                return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session et cet étudiant." });
            }

            bool isAuthorized = false;
            bool isProfToken = false;
            var isAdmin = false;
            var isDelegate = false;

            string? profTokenValue = null;

            if (Request.Headers.TryGetValue("Prof-Signature-Token", out var headerToken))
            {
                profTokenValue = headerToken.ToString();
                _logger.LogInformation($"Token trouvé dans l'en-tête HTTP: {profTokenValue}");
            }
            else if (Request.Query.TryGetValue("token", out var queryToken))
            {
                profTokenValue = queryToken.ToString();
                _logger.LogInformation($"Token trouvé dans l'URL: {profTokenValue}");
            }
            else
            {
                var path = Request.Path.Value;
                if (!string.IsNullOrEmpty(path) && path.Contains("/prof-signature/"))
                {
                    var segments = path.Split('/');
                    for (int i = 0; i < segments.Length; i++)
                    {
                        if (segments[i] == "prof-signature" && i + 1 < segments.Length)
                        {
                            profTokenValue = segments[i + 1];
                            _logger.LogInformation($"Token trouvé dans le chemin: {profTokenValue}");
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(profTokenValue) && profTokenValue != session.ProfSignatureToken)
            {
                var sessionByToken = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.ProfSignatureToken == profTokenValue);

                if (sessionByToken != null)
                {
                    session = sessionByToken;
                    sessionId = session.Id;
                    _logger.LogInformation($"Session trouvée par token: {sessionId}");

                    attendance = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == user.Id);

                    if (attendance == null)
                    {
                        return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session et cet étudiant." });
                    }
                }
            }

            if (!string.IsNullOrEmpty(profTokenValue) && profTokenValue == session.ProfSignatureToken)
            {
                isAuthorized = true;
                isProfToken = true;
                _logger.LogInformation($"Accès autorisé par token professeur pour consulter la présence de {studentNumber} dans la session {sessionId}");
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                var isAdminClaim = User.FindFirstValue("role");
                var isDelegateClaim = User.FindFirstValue("isDelegate");
                var userStudentNumber = User.FindFirstValue("studentNumber");

                isAdmin = isAdminClaim == "Admin";
                isDelegate = isDelegateClaim == "true";
                bool isOwnAttendance = userStudentNumber == studentNumber;

                if (isAdmin || isDelegate || isOwnAttendance)
                {
                    isAuthorized = true;
                }

                _logger.LogInformation($"User {userStudentNumber} requesting attendance info - Role: {isAdminClaim}, IsDelegate: {isDelegateClaim}");
                _logger.LogInformation($"Interpreted values - IsAdmin: {isAdmin}, IsDelegate: {isDelegate}");
            }
            else if (!isProfToken)
            {
                _logger.LogInformation("No authenticated user found for attendance request");
            }

            if (!isAuthorized)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé à la présence de {studentNumber} pour la session {sessionId}");
                return Forbid();
            }

            var attendanceDto = new
            {
                attendance.Id,
                attendance.SessionId,
                attendance.StudentId,
                attendance.Status,
            };

            return Ok(attendanceDto);
        }

        /**
         * GetSessionAttendances
         *
         * This method retrieves all attendances for a specific session.
         */
        [HttpGet("{sessionId}/attendances")]
        public async Task<IActionResult> GetSessionAttendances(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);

            if (session == null)
            {
                return NotFound(new { error = true, message = $"Session avec l'ID {sessionId} non trouvée." });
            }

            var attendances = await _context.Attendances
                .Where(a => a.SessionId == sessionId)
                .Include(a => a.User)
                .ToListAsync();

            if (attendances == null || attendances.Count == 0)
            {
                return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session." });
            }

            var result = new List<dynamic>();

            foreach (var attendance in attendances)
            {
                var student = await _context.Users.FindAsync(attendance.StudentId);
                if (student != null)
                {
                    result.Add(new
                    {
                        item1 = new
                        {
                            id = student.Id,
                            name = student.Name,
                            firstname = student.Firstname,
                            studentNumber = student.StudentNumber,
                            signature = student.Signature,
                            comment = attendance.Comment
                        },
                        item2 = attendance.Status
                    });
                }
            }

            if (result.Count == 0)
            {
                return NotFound(new { error = true, message = "Aucun étudiant trouvé pour cette session." });
            }
            return Ok(result);
        }

        /**
         * StreamSessionAttendances (SSE)
         *
         * Flux Server-Sent Events qui pousse la liste des présences d'une session
         * dès qu'elle change (un étudiant émarge, le prof bascule un statut, etc.).
         * Utilisé par la page de signature professeur pour une mise à jour automatique.
         * Accès public via le lien tokenisé (route en "/attendances/" laissée passer par le middleware).
         */
        [HttpGet("{sessionId}/attendances/stream")]
        public async Task StreamSessionAttendances(int sessionId, CancellationToken cancellationToken)
        {
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no"; // évite le buffering par nginx

            var jsonOptions = new System.Text.Json.JsonSerializerOptions();
            string? lastJson = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string json;
                    // Scope/DbContext frais à chaque itération : la connexion SSE est longue,
                    // on ne garde pas un DbContext vivant pendant toute sa durée.
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var data = await db.Attendances
                            .AsNoTracking()
                            .Where(a => a.SessionId == sessionId)
                            .Include(a => a.User)
                            .OrderBy(a => a.User.Name).ThenBy(a => a.User.Firstname)
                            .Select(a => new
                            {
                                item1 = new
                                {
                                    id = a.User.Id,
                                    name = a.User.Name,
                                    firstname = a.User.Firstname,
                                    studentNumber = a.User.StudentNumber,
                                    comment = a.Comment
                                },
                                item2 = (int)a.Status
                            })
                            .ToListAsync(cancellationToken);
                        json = System.Text.Json.JsonSerializer.Serialize(data, jsonOptions);
                    }

                    if (json != lastJson)
                    {
                        lastJson = json;
                        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    }
                    else
                    {
                        // Commentaire SSE = heartbeat, garde la connexion ouverte.
                        await Response.WriteAsync(": ping\n\n", cancellationToken);
                    }
                    await Response.Body.FlushAsync(cancellationToken);

                    await Task.Delay(2000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Client déconnecté : fin normale du flux.
            }
        }

        /**
         * SaveSignature
         *
         * This method saves the signature for a student.
         * Users can only modify their own signature unless they are administrators.
         */
        [HttpPost("signature/{studentNumber}")]
        [Authorize]
        public async Task<IActionResult> SaveSignature(string studentNumber, [FromBody] SignatureModel signatureData)
        {
            // Récupère l'ID de l'utilisateur authentifié
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            // Récupère l'utilisateur authentifié
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            // Récupère l'étudiant dont on veut modifier la signature
            var student = await _context.Users
                .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);
            if (student == null)
            {
                return NotFound(new { error = true, message = "Aucun étudiant trouvé avec les identifiants fournis." });
            }

            // Vérifie si l'utilisateur connecté modifie sa propre signature ou est administrateur
            if (currentUser.StudentNumber != student.StudentNumber && !currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé: {currentUser.StudentNumber} a tenté de modifier la signature de {studentNumber}");
                return Forbid();
            }

            student.Signature = signatureData.Signature;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Signature enregistrée avec succès." });
        }

        /**
         * GetSignature
         *
         * This method retrieves the signature for a student.
         */
        [HttpGet("signature/{studentNumber}")]
        public async Task<IActionResult> GetSignature(string studentNumber)
        {
            var student = await _context.Users
                .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);

            if (student == null)
            {
                return NotFound(new { error = true, message = "Aucun étudiant trouvé avec les identifiants fournis." });
            }

            return Ok(new { signature = student.Signature });
        }

        /**
         * GetNotSendSessions
         *
         * This method retrieves all sessions that have not been sent.
         * Le code de validation n'est inclus que si l'utilisateur est un délégué ou un administrateur.
         */
        [HttpGet("not-send")]
        public async Task<IActionResult> GetNotSendSessions()
        {
            var sessions = await _context.Sessions
                .Where(s => !s.IsSent)
                .ToListAsync();

            if (sessions == null || sessions.Count == 0)
            {
                return NotFound(new { message = "Aucune session trouvée." });
            }

            var isAdmin = false;
            var isDelegate = false;

            if (User.Identity?.IsAuthenticated == true &&
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                var currentUser = await _context.Users.FindAsync(userId);
                if (currentUser != null)
                {
                    isAdmin = currentUser.IsAdmin;
                    isDelegate = currentUser.IsDelegate;
                }
            }

            if (!isAdmin && !isDelegate)
            {
                var sessionsWithoutCode = sessions.Select(s => new
                {
                    s.Id,
                    s.Date,
                    s.StartTime,
                    s.EndTime,
                    s.Year,
                    s.Name,
                    s.Room,
                    s.ProfId,
                    s.ProfSignature,
                    s.ProfSignatureToken,
                    s.ProfId2,
                    s.ProfSignature2,
                    s.ProfSignatureToken2,
                    s.IsSent,
                    s.IsMailSent,
                    s.IsMailSent2
                }).ToList();

                return Ok(sessionsWithoutCode);
            }

            return Ok(sessions);
        }

        /**
         * SetProfEmail
         *
         * This method sets the professor's email for a session.
         */
        [HttpPost("{sessionId}/set-prof-email")]
        public async Task<IActionResult> SetProfEmail(int sessionId, [FromBody] SetProfEmailModel model)
        {
            _logger.LogDebug($"Tentative de mise à jour de l'email du professeur pour la session {sessionId}");
            _logger.LogDebug($"Email du professeur : {model.ProfEmail}");
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
                return NotFound(new { error = true, message = "Session non trouvée." });
            if (!int.TryParse(session.ProfId, out int profId))
                return NotFound(new { error = true, message = "Professeur non trouvé (ID invalide)." });
            var professor = await _context.Users.FirstOrDefaultAsync(u => u.Id == profId && u.IsProfessor);
            if (professor == null)
                return NotFound(new { error = true, message = "Professeur non trouvé." });
            professor.Email = model.ProfEmail;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Email du professeur 1 enregistré" });
        }

        /**
         * SetProf2Email
         *
         * This method sets the second professor's email for a session.
         */
        [HttpPost("{sessionId}/set-prof2-email")]
        public async Task<IActionResult> SetProf2Email(int sessionId, [FromBody] SetProfEmailModel model)
        {
            _logger.LogDebug($"Tentative de mise à jour de l'email du professeur 2 pour la session {sessionId}");
            _logger.LogDebug($"Email du professeur 2 : {model.ProfEmail}");
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
                return NotFound(new { error = true, message = "Session non trouvée." });
            if (!int.TryParse(session.ProfId2, out int profId2))
                return NotFound(new { error = true, message = "Professeur 2 non trouvé (ID invalide)." });
            var professor2 = await _context.Users.FirstOrDefaultAsync(u => u.Id == profId2 && u.IsProfessor);
            if (professor2 == null)
                return NotFound(new { error = true, message = "Professeur 2 non trouvé." });
            professor2.Email = model.ProfEmail;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Email du professeur 2 enregistré et mail envoyé." });
        }

        /**
         * SetSessionProfessor
         *
         * This method assigns or removes a professor on a given session slot (1 or 2).
         * Passing null ProfessorId removes the professor from the slot.
         */
        [HttpPost("{sessionId}/set-professor/{slot}")]
        [Authorize]
        public async Task<IActionResult> SetSessionProfessor(int sessionId, int slot, [FromBody] SetSessionProfessorModel model)
        {
            if (slot is not 1 and not 2)
                return BadRequest(new { error = true, message = "Le slot professeur doit être 1 ou 2." });

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
                return NotFound(new { message = "Utilisateur connecté introuvable." });

            if (!currentUser.IsAdmin && !currentUser.IsDelegate)
                return Forbid();

            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
                return NotFound(new { error = true, message = "Session non trouvée." });

            if (!model.ProfessorId.HasValue)
            {
                if (slot == 1)
                {
                    session.ProfId = null;
                }
                else
                {
                    session.ProfId2 = null;
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Professeur retiré du créneau." });
            }

            var professor = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.ProfessorId.Value && u.IsProfessor);
            if (professor == null)
                return NotFound(new { error = true, message = "Professeur non trouvé." });

            var professorId = professor.Id.ToString();
            if ((slot == 1 && session.ProfId2 == professorId) || (slot == 2 && session.ProfId == professorId))
                return Conflict(new { error = true, message = "Ce professeur est déjà affecté à l'autre créneau." });

            if (slot == 1)
            {
                session.ProfId = professorId;
                session.ProfSignature = null;
                session.ProfSignatureToken = Guid.NewGuid().ToString();
                session.IsMailSent = false;
            }
            else
            {
                session.ProfId2 = professorId;
                session.ProfSignature2 = null;
                session.ProfSignatureToken2 = Guid.NewGuid().ToString();
                session.IsMailSent2 = false;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Professeur de session mis à jour." });
        }

        /**
         * ResendProfMail
         *
         * This method resends the email to the professor for a session.
         */
        [HttpPost("{sessionId}/resend-prof-mail")]
        public async Task<IActionResult> ResendProfMail(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null || string.IsNullOrEmpty(session.ProfId))
                return NotFound(new { error = true, message = "Session ou email du professeur 1 non trouvé." });
            await SendProfSignatureMail(session, 1);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Mail renvoyé au professeur 1." });
        }

        /**
         * ResendProf2Mail
         *
         * This method resends the email to the second professor for a session.
         */
        [HttpPost("{sessionId}/resend-prof2-mail")]
        public async Task<IActionResult> ResendProf2Mail(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null || string.IsNullOrEmpty(session.ProfId2))
                return NotFound(new { error = true, message = "Session ou email du professeur 2 non trouvé." });
            await SendProfSignatureMail(session, 2);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Mail renvoyé au professeur 2." });
        }



        /**
         * SendProfSignatureMail
         *
         * This method sends an email to the professor for signing the attendance sheet.
         */
        private async Task SendProfSignatureMail(Session session)
        {
            // Par défaut, envoi au premier professeur
            await SendProfSignatureMail(session, 1);
        }

        /// <summary>
        /// Indique si le professeur (identifié par son Id sous forme de chaîne) a choisi
        /// de consulter ses cours depuis son espace connecté plutôt que de recevoir un mail.
        /// </summary>
        private async Task<bool> ProfessorUsesAccountMode(string? profId)
        {
            if (!int.TryParse(profId, out var id)) return false;
            var professor = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsProfessor);
            return professor != null && professor.NotificationMode == UserNotificationMode.Account;
        }

        /**
         * SendProfSignatureMail
         *
         * This method sends an email to a specific professor for signing the attendance sheet.
         * @param session The session
         * @param professorNumber 1 for first professor, 2 for second professor
         */
        private async Task SendProfSignatureMail(Session session, int professorNumber)
        {
            string? profEmail;
            string? profSignatureToken;
            string profName;

            if (professorNumber == 1)
            {
                var profIdInt = int.TryParse(session.ProfId, out var id1) ? id1 : 0;
                var professor = await _context.Users.FirstOrDefaultAsync(u => u.Id == profIdInt && u.IsProfessor);
                profEmail = professor?.Email;
                profSignatureToken = session.ProfSignatureToken;
                profName = $"{professor?.Firstname} {professor?.Name}";
            }
            else if (professorNumber == 2)
            {
                var profId2Int = int.TryParse(session.ProfId2, out var id2) ? id2 : 0;
                var professor2 = await _context.Users.FirstOrDefaultAsync(u => u.Id == profId2Int && u.IsProfessor);
                profEmail = professor2?.Email;
                profSignatureToken = session.ProfSignatureToken2;
                profName = $"{professor2?.Firstname} {professor2?.Name}";
            }
            else
            {
                throw new ArgumentException("Le numéro de professeur doit être 1 ou 2", nameof(professorNumber));
            }

            if (session == null || string.IsNullOrWhiteSpace(profEmail) || string.IsNullOrWhiteSpace(profSignatureToken))
                return;

            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
            var link = $"{frontendUrl}/prof-signature/{profSignatureToken}";
            var subject = "Signature de la feuille de présence";
            var body = $@"Bonjour {profName},

Veuillez cliquer sur le lien suivant pour renseigner votre nom, prénom et signer la feuille de présence :
{link}

Informations de la session :
- Année : {session.Year}
- Date : {session.Date:yyyy/MM/dd}
- Heure de début : {session.StartTime.ToString(@"HH\:mm")} - Heure de fin : {session.EndTime.ToString(@"HH\:mm")}
- Validation : {session.ValidationCode}

Cordialement";

            // Mise en file : l'envoi SMTP réel est fait par EmailDispatcherService.
            // La persistance de cette ligne est assurée par le SaveChanges de l'appelant.
            _context.OutboxEmails.Add(new OutboxEmail
            {
                ToEmail = profEmail,
                Subject = subject,
                Body = body,
                IsHtml = false,
            });
        }

        /**
         * SetProfEmailModel
         *
         * This model is used for setting the professor's email.
         */
        public class SetProfEmailModel
        {
            public string ProfEmail { get; set; } = string.Empty;
        }

        public class SetSessionProfessorModel
        {
            public int? ProfessorId { get; set; }
        }

        /**
         * SignatureModel
         *
         * This model is used for saving the professor's signature.
         */
        public class SignatureModel
        {
            public string Signature { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Firstname { get; set; } = string.Empty;
        }

        /**
         * SessionExists
         *
         * This method checks if a session exists in the database.
         */
        private bool SessionExists(int id)
        {
            return _context.Sessions.Any(e => e.Id == id);
        }

        /**
         * ChangeAttendanceStatus
         *
         * This method changes the attendance status for a student in a session.
         * Accessible ONLY via:
         * - Authenticated users (admin only)
         * - Professors with valid signature token (without authentication)
         */
        [HttpPost("{sessionId}/attendance-status/{studentNumber}")]
        public async Task<IActionResult> ChangeAttendanceStatus(int sessionId, string studentNumber, [FromBody] ChangeAttendanceStatusModel model)
        {
            _logger.LogInformation($"==== DÉBUT TRAITEMENT REQUÊTE CHANGEMENT STATUT ====");
            _logger.LogInformation($"Requête reçue pour modifier le statut de présence: Session={sessionId}, Étudiant={studentNumber}");

            _logger.LogInformation("TOUS LES EN-TÊTES:");
            foreach (var header in Request.Headers)
            {
                _logger.LogInformation($"  {header.Key}: {header.Value}");
            }

            if (model != null)
            {
                _logger.LogInformation($"Modèle reçu: Status={model.Status}, ProfSignatureToken={model.ProfSignatureToken ?? "null"}");
            }
            else
            {
                _logger.LogInformation("Modèle reçu: null");
            }

            string? profHeaderToken = null;
            if (Request.Headers.TryGetValue("Prof-Signature-Token", out var headerTokenValue))
            {
                profHeaderToken = headerTokenValue.ToString();
                _logger.LogInformation($"Token trouvé dans l'en-tête HTTP: {profHeaderToken}");
            }
            else
            {
                _logger.LogWarning("Aucun token trouvé dans l'en-tête Prof-Signature-Token");
            }

            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = true, message = $"Session avec l'ID {sessionId} non trouvée." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (user == null)
            {
                return NotFound(new { error = true, message = "Aucun utilisateur trouvé avec les identifiants fournis." });
            }

            bool isAuthorized = false;

            string? profTokenValue = profHeaderToken;

            if (string.IsNullOrEmpty(profTokenValue))
            {
                if (Request.Query.TryGetValue("token", out var queryToken))
                {
                    profTokenValue = queryToken.ToString();
                    _logger.LogInformation($"Token trouvé dans l'URL: {profTokenValue}");
                }
                else if (model != null && !string.IsNullOrEmpty(model.ProfSignatureToken))
                {
                    profTokenValue = model.ProfSignatureToken;
                    _logger.LogInformation($"Token trouvé dans le body: {profTokenValue}");
                }
            }

            if (!string.IsNullOrEmpty(profTokenValue))
            {
                if (profTokenValue != session.ProfSignatureToken && profTokenValue != session.ProfSignatureToken2)
                {
                    var sessionByToken = await _context.Sessions
                        .FirstOrDefaultAsync(s => s.ProfSignatureToken == profTokenValue || s.ProfSignatureToken2 == profTokenValue);

                    if (sessionByToken != null)
                    {
                        session = sessionByToken;
                        sessionId = session.Id;
                        _logger.LogInformation($"Session trouvée par token: {sessionId}");
                    }
                }

                if (profTokenValue == session.ProfSignatureToken || profTokenValue == session.ProfSignatureToken2)
                {
                    isAuthorized = true;
                    _logger.LogInformation($"Accès autorisé par token professeur (1 ou 2) pour modifier le statut de présence de {studentNumber} dans la session {sessionId}");
                }
            }

            if (!isAuthorized && User.Identity?.IsAuthenticated == true)
            {
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId);
                    if (currentUser != null && currentUser.IsAdmin)
                    {
                        isAuthorized = true;
                        _logger.LogInformation($"Accès autorisé pour administrateur {currentUser.StudentNumber} pour modifier le statut de présence de {studentNumber}");
                    }
                    else
                    {
                        _logger.LogWarning($"Utilisateur {currentUser?.StudentNumber} non autorisé (non admin) pour modifier le statut de présence");
                    }
                }
            }

            if (!isAuthorized)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé pour modifier le statut de présence de {studentNumber}");
                _logger.LogInformation($"Résumé de l'autorisation: Utilisateur authentifié: {User.Identity?.IsAuthenticated}, ProfToken dans l'en-tête: {!string.IsNullOrEmpty(profHeaderToken)}, ProfToken dans le body: {model?.ProfSignatureToken != null}");
                _logger.LogInformation($"Token attendu pour la session {sessionId}: {session.ProfSignatureToken}");
                return Forbid();
            }

            _logger.LogInformation($"Accès autorisé pour modification de présence, méthode: {(User.Identity?.IsAuthenticated == true ? "Authentification utilisateur" : "Token signature professeur")}");

            var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == user.Id);
            if (attendance == null)
            {
                return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session et cet étudiant." });
            }

            if (model == null)
            {
                return BadRequest(new { error = true, message = "Données de statut invalides." });
            }

            attendance.Status = (AttendanceStatus)model.Status;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Statut de présence mis à jour avec succès." });
        }

        /**
         * ChangeAttendanceStatusModel
         *
         * This model is used for changing the attendance status.
         */
        public class ChangeAttendanceStatusModel
        {
            public int Status { get; set; }
            public string? ProfSignatureToken { get; set; }
        }

        /**
         * GetTimers
         *
         * This method retrieves the next execution times for the import and mail timers.
         */
        [HttpGet("timers")]
        public IActionResult GetTimers()
        {
            var importTime = backend.Services.TimerService.StaticNextSessionExecutionTime;
            var mailTime = backend.Services.TimerService.StaticNextMailExecutionTime;
            var now = DateTime.Now;
            var importRemaining = importTime - now;
            return Ok(new
            {
                nextImport = importTime,
                importRemaining = importRemaining.ToString(@"hh\:mm\:ss"),
                nextMail = mailTime,
                autoImportEnabled = backend.Services.TimerService.IsAutoImportEnabled
            });
        }

        /**
         * GetAutoImportStatus
         *
         * This method retrieves the status of automatic EDT import.
         */
        [HttpGet("auto-import-status")]
        public IActionResult GetAutoImportStatus()
        {
            return Ok(new
            {
                enabled = backend.Services.TimerService.IsAutoImportEnabled
            });
        }

        /**
         * SetAutoImportStatus
         *
         * This method enables or disables automatic EDT import.
         */
        [HttpPost("auto-import-status")]
        public IActionResult SetAutoImportStatus([FromBody] AutoImportStatusModel model)
        {
            if (model.Enabled)
            {
                backend.Services.TimerService.EnableAutoImport(_logger);
            }
            else
            {
                backend.Services.TimerService.DisableAutoImport(_logger);
            }

            return Ok(new
            {
                enabled = backend.Services.TimerService.IsAutoImportEnabled,
                message = model.Enabled ? "Import automatique activé" : "Import automatique désactivé"
            });
        }

        public class AutoImportStatusModel
        {
            public bool Enabled { get; set; }
        }


        /**
         * UpdateAttendanceComment
         *
         * This method updates the comment for a student's attendance in a session.
         * Accessible via:
         * - Authenticated users (own attendance, admin or delegate)
         * - Professors with valid signature token (without authentication)
         */
        [HttpPost("{sessionId}/attendance-comment/{studentNumber}")]
        public async Task<IActionResult> UpdateAttendanceComment(int sessionId, string studentNumber, [FromBody] CommentUpdateModel model)
        {
            _logger.LogInformation($"Requête reçue pour modifier le commentaire: Session={sessionId}, Étudiant={studentNumber}");
            _logger.LogInformation($"En-têtes: {string.Join(", ", Request.Headers.Select(h => $"{h.Key}={h.Value}"))}");

            // Log des détails de la requête pour aider au débogage
            if (model != null)
            {
                _logger.LogInformation($"Modèle reçu: Comment={model.Comment}, ProfSignatureToken={model.ProfSignatureToken ?? "null"}");
            }
            else
            {
                _logger.LogInformation("Modèle reçu: null");
            }

            string? profHeaderToken = null;
            if (Request.Headers.TryGetValue("Prof-Signature-Token", out var headerTokenValue))
            {
                profHeaderToken = headerTokenValue.ToString();
                _logger.LogInformation($"Token trouvé dans l'en-tête HTTP: {profHeaderToken}");
            }
            var sessionNormal = await _context.Sessions.FindAsync(sessionId);
            if (sessionNormal == null)
            {
                return NotFound(new { error = true, message = $"Session avec l'ID {sessionId} non trouvée." });
            }

            var userNormal = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (userNormal == null)
            {
                return NotFound(new { error = true, message = "Aucun utilisateur trouvé avec les identifiants fournis." });
            }

            bool isAuthorized = false;
            string accessType = "non autorisé";

            string? profTokenValue = null;

            if (Request.Headers.TryGetValue("Prof-Signature-Token", out var headerToken))
            {
                profTokenValue = headerToken.ToString();
                _logger.LogInformation($"Token trouvé dans l'en-tête HTTP: {profTokenValue}");
            }
            else if (Request.Query.TryGetValue("token", out var queryToken))
            {
                profTokenValue = queryToken.ToString();
                _logger.LogInformation($"Token trouvé dans l'URL: {profTokenValue}");
            }
            else if (model != null && !string.IsNullOrEmpty(model.ProfSignatureToken))
            {
                profTokenValue = model.ProfSignatureToken;
                _logger.LogInformation($"Token trouvé dans le body: {profTokenValue}");
            }
            else if (Request.Headers.TryGetValue("Referer", out var referer))
            {
                string refererStr = referer.ToString();
                _logger.LogInformation($"Recherche du token dans le Referer: {refererStr}");

                if (!string.IsNullOrEmpty(refererStr) && refererStr.Contains("/prof-signature/"))
                {
                    try
                    {
                        var refererUri = new Uri(refererStr);
                        var segments = refererUri.AbsolutePath.Split('/');
                        for (int i = 0; i < segments.Length; i++)
                        {
                            if (segments[i] == "prof-signature" && i + 1 < segments.Length)
                            {
                                profTokenValue = segments[i + 1];
                                _logger.LogInformation($"Token trouvé dans le Referer: {profTokenValue}");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Erreur lors de l'analyse du Referer: {ex.Message}");
                    }
                }
            }

            if (string.IsNullOrEmpty(profTokenValue))
            {
                _logger.LogInformation("Recherche de toutes les sessions récentes...");
                var today = DateTime.Today;
                var sessions = await _context.Sessions
                    .Where(s => s.Date >= today.AddDays(-1) && s.Date <= today.AddDays(1))
                    .ToListAsync();

                foreach (var s in sessions)
                {
                    _logger.LogInformation($"Session ID={s.Id}, Date={s.Date}, ProfSignatureToken={s.ProfSignatureToken}");
                }

                var matchingSession = sessions.FirstOrDefault(s => s.Id == sessionId);
                if (matchingSession != null)
                {
                    profTokenValue = matchingSession.ProfSignatureToken;
                    _logger.LogInformation($"Utilisation du token de la session trouvée: {profTokenValue}");
                }
            }

            if (!string.IsNullOrEmpty(profTokenValue) && profTokenValue != sessionNormal.ProfSignatureToken && profTokenValue != sessionNormal.ProfSignatureToken2)
            {
                var sessionByToken = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.ProfSignatureToken == profTokenValue || s.ProfSignatureToken2 == profTokenValue);

                if (sessionByToken != null)
                {
                    sessionNormal = sessionByToken;
                    sessionId = sessionNormal.Id;
                    _logger.LogInformation($"Session trouvée par token: {sessionId}");
                }
            }

            if (!string.IsNullOrEmpty(profTokenValue) && (profTokenValue == sessionNormal.ProfSignatureToken || profTokenValue == sessionNormal.ProfSignatureToken2))
            {
                isAuthorized = true;
                accessType = "prof-token";
                _logger.LogInformation($"Accès autorisé par token professeur (1 ou 2) pour modifier le commentaire de présence de {studentNumber} dans la session {sessionId}");
            }
            else if (User.Identity?.IsAuthenticated == true)
            {
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
                {
                    var currentUser = await _context.Users.FindAsync(currentUserId);
                    if (currentUser != null)
                    {
                        bool isOwnAttendance = currentUser.Id == userNormal.Id;
                        if (currentUser.IsAdmin || currentUser.IsDelegate || isOwnAttendance)
                        {
                            isAuthorized = true;
                            accessType = currentUser.IsAdmin ? "admin" : (currentUser.IsDelegate ? "délégué" : "utilisateur");
                            _logger.LogInformation($"Accès autorisé pour {accessType} {currentUser.StudentNumber} pour modifier le commentaire de présence de {studentNumber}");
                        }
                    }
                }
            }

            if (!isAuthorized)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé pour modifier le commentaire de présence de {studentNumber}");
                return Forbid();
            }

            var attendanceNormal = await _context.Attendances
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == userNormal.Id);
            if (attendanceNormal == null)
            {
                return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session et cet étudiant." });
            }

            if (model != null)
            {
                attendanceNormal.Comment = model.Comment;
                await _context.SaveChangesAsync();
            }
            else
            {
                return BadRequest(new { error = true, message = "Données de commentaire invalides." });
            }

            return Ok(new { message = "Commentaire mis à jour avec succès." });
        }

        /**
         * CommentUpdateModel
         *
         * This model is used for updating the attendance comment.
         */
        public class CommentUpdateModel
        {
            public string Comment { get; set; } = string.Empty;
            public string? ProfSignatureToken { get; set; }
        }
    }
}