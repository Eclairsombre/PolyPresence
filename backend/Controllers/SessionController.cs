using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net;
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
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public SessionController(ApplicationDbContext context, ILogger<SessionController> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /**
         * ImportAllIcsLinks
         *
         * This method imports all ICS links from the database and processes them.
         */
        public async Task ImportAllIcsLinks(ApplicationDbContext context, ILogger logger)
        {
            var importStartTime = DateTime.Now;
            logger.LogInformation($"====== DÉBUT IMPORT GLOBAL ICS à {importStartTime:yyyy-MM-dd HH:mm:ss} ======");

            var icsLinks = await context.IcsLinks.ToListAsync();
            logger.LogInformation($"Nombre de liens ICS à importer : {icsLinks.Count}");

            foreach (var link in icsLinks)
            {
                try
                {
                    logger.LogInformation($"Import du lien ICS : {link.Url} pour l'année {link.Year}");
                    var importModel = new ImportIcsModel { IcsUrl = link.Url, Year = link.Year };
                    var controller = new SessionController(context, (ILogger<SessionController>)logger, _serviceScopeFactory);
                    await controller.ImportIcs(importModel);
                    logger.LogInformation($"Import terminé pour {link.Url}");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Erreur lors de l'import ICS pour {link.Url} : {ex.Message}");
                }
            }

            var importEndTime = DateTime.Now;
            var duration = importEndTime - importStartTime;
            logger.LogInformation($"====== FIN IMPORT GLOBAL ICS à {importEndTime:yyyy-MM-dd HH:mm:ss} (durée: {duration.TotalSeconds:F2}s) ======");
        }

        /**
         * CheckAndSendSessionMails
         *
         * This method checks for sessions that need to send emails to professors and sends them.
         */
        public async Task CheckAndSendSessionMails()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionController>>();
                var now = DateTime.Now;
                _logger.LogInformation($"Vérification des sessions à {now} pour l'envoi de mails aux professeurs.");

                var sessions = await scopedContext.Sessions
                    .Where(s => s.Date == now.Date &&
                               ((!s.IsMailSent && !string.IsNullOrEmpty(s.ProfEmail)) ||
                                (!s.IsMailSent2 && !string.IsNullOrEmpty(s.ProfEmail2))))
                    .ToListAsync();

                sessions = sessions
                    .Where(s => s.StartTime <= now.TimeOfDay)
                    .ToList();

                foreach (var session in sessions)
                {
                    if (!session.IsMailSent && !string.IsNullOrEmpty(session.ProfEmail))
                    {
                        try
                        {
                            await SendProfSignatureMail(session, 1);
                            session.IsMailSent = true;
                            await scopedContext.SaveChangesAsync();
                            logger.LogInformation($"Mail envoyé automatiquement au professeur 1 pour la session {session.Id}.");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError($"Erreur lors de l'envoi automatique du mail au professeur 1 pour la session {session.Id} : {ex.Message}");
                        }
                    }

                    if (!session.IsMailSent2 && !string.IsNullOrEmpty(session.ProfEmail2))
                    {
                        try
                        {
                            await SendProfSignatureMail(session, 2);
                            session.IsMailSent2 = true;
                            await scopedContext.SaveChangesAsync();
                            logger.LogInformation($"Mail envoyé automatiquement au professeur 2 pour la session {session.Id}.");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError($"Erreur lors de l'envoi automatique du mail au professeur 2 pour la session {session.Id} : {ex.Message}");
                        }
                    }
                }
            }
        }

        /**
         * GetAllSessions
         *
         * This method retrieves all sessions from the database.
         * Le code de validation n'est inclus que si l'utilisateur est un délégué ou un administrateur.
         */
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetSessions()
        {
            var sessions = await _context.Sessions.ToListAsync();

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
                    s.ProfName,
                    s.ProfFirstname,
                    s.ProfEmail,
                    s.ProfSignature,
                    s.ProfSignatureToken,
                    s.ProfName2,
                    s.ProfFirstname2,
                    s.ProfEmail2,
                    s.ProfSignature2,
                    s.ProfSignatureToken2,
                    s.IsSent,
                    s.IsMailSent,
                    s.IsMailSent2
                }).ToList();

                return sessionsWithoutCode;
            }

            return sessions;
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
            var session = await _context.Sessions.FindAsync(id);

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
                    session.ProfName,
                    session.ProfFirstname,
                    session.ProfEmail,
                    session.ProfSignature,
                    session.ProfSignatureToken,
                    session.ProfName2,
                    session.ProfFirstname2,
                    session.ProfEmail2,
                    session.ProfSignature2,
                    session.ProfSignatureToken2,
                    session.IsSent,
                    session.IsMailSent,
                    session.IsMailSent2
                });
            }

            _logger.LogInformation($"Returning full session {id} with validation code");
            return session;
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
                    s.ProfName,
                    s.ProfFirstname,
                    s.ProfEmail,
                    s.ProfSignature,
                    s.ProfSignatureToken,
                    s.ProfName2,
                    s.ProfFirstname2,
                    s.ProfEmail2,
                    s.ProfSignature2,
                    s.ProfSignatureToken2,
                    s.IsSent,
                    s.IsMailSent,
                    s.IsMailSent2
                }).ToList();

                return sessionsWithoutCode;
            }

            return sessions;
        }

        /**
         * PostSession
         *
         * This method creates a new session in the database.
         */
        [HttpPost]
        public async Task<ActionResult<Session>> PostSession(Session session)
        {
            session.ProfSignatureToken = Guid.NewGuid().ToString();

            if (!string.IsNullOrEmpty(session.ProfName2) || !string.IsNullOrEmpty(session.ProfFirstname2))
            {
                session.ProfSignatureToken2 = Guid.NewGuid().ToString();
            }

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
                if (!string.IsNullOrWhiteSpace(signatureData.Name))
                    session.ProfName = signatureData.Name;
                if (!string.IsNullOrWhiteSpace(signatureData.Firstname))
                    session.ProfFirstname = signatureData.Firstname;
            }
            else if (session.ProfSignatureToken2 == token)
            {
                session.ProfSignature2 = signatureData.Signature;
                if (!string.IsNullOrWhiteSpace(signatureData.Name))
                    session.ProfName2 = signatureData.Name;
                if (!string.IsNullOrWhiteSpace(signatureData.Firstname))
                    session.ProfFirstname2 = signatureData.Firstname;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Signature du professeur enregistrée avec succès." });
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

        /**
         * GetCurrentSession
         *
         * This method retrieves the current session for a given year.
         * Le code de validation n'est inclus que si l'utilisateur est un délégué ou un administrateur.
         */
        [HttpGet("current/{year}")]
        public async Task<ActionResult<object>> GetCurrentSession(string year)
        {
            var today = DateTime.Today;
            var now = DateTime.Now.TimeOfDay;

            var sessionsToday = await _context.Sessions
                .Where(s => s.Year == year && s.Date == today)
                .ToListAsync();

            var currentSession = sessionsToday
                .FirstOrDefault(s => s.StartTime <= now && s.EndTime >= now);

            if (currentSession == null)
            {
                return NotFound(new { message = $"Aucune session trouvée pour l'année {year} aujourd'hui pour l'heure {now}." });
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
                _logger.LogInformation($"User {userStudentNumber} requesting current session - Role: {isAdminClaim}, IsDelegate: {isDelegateClaim}");
                _logger.LogInformation($"Interpreted values - IsAdmin: {isAdmin}, IsDelegate: {isDelegate}");
            }
            else
            {
                _logger.LogInformation("No authenticated user found for current session request");
            }

            if (!isAdmin && !isDelegate)
            {
                _logger.LogInformation("Hiding validation code in current session response");

                return new ActionResult<object>(new
                {
                    currentSession.Id,
                    currentSession.Date,
                    currentSession.StartTime,
                    currentSession.EndTime,
                    currentSession.Year,
                    currentSession.Name,
                    currentSession.Room,
                    currentSession.ProfName,
                    currentSession.ProfFirstname,
                    currentSession.ProfEmail,
                    currentSession.IsSent,
                    currentSession.IsMailSent
                });
            }

            _logger.LogInformation("Returning full current session with validation code");
            return currentSession;
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

            // Vérification du code de validation
            if (model.ValidationCode != session.ValidationCode)
            {
                _logger.LogWarning($"Tentative de validation avec un code incorrect: {model.ValidationCode} pour la session {sessionId}");
                return BadRequest(new { error = true, message = "Le code de validation est incorrect." });
            }

            if (session.Date != DateTime.Today)
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
                    s.ProfName,
                    s.ProfFirstname,
                    s.ProfEmail,
                    s.ProfSignature,
                    s.ProfSignatureToken,
                    s.ProfName2,
                    s.ProfFirstname2,
                    s.ProfEmail2,
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
            session.ProfEmail = model.ProfEmail;
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
            session.ProfEmail2 = model.ProfEmail;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Email du professeur 2 enregistré et mail envoyé." });
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
            if (session == null || string.IsNullOrEmpty(session.ProfEmail))
                return NotFound(new { error = true, message = "Session ou email du professeur 1 non trouvé." });
            await SendProfSignatureMail(session, 1);
            return Ok(new { message = "Mail renvoyé au professeur 1." });
        }

        /**
         * ResendProf1Mail
         *
         * This method resends the email to the first professor for a session.
         */
        [HttpPost("{sessionId}/resend-prof1-mail")]
        public async Task<IActionResult> ResendProf1Mail(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null || string.IsNullOrEmpty(session.ProfEmail))
                return NotFound(new { error = true, message = "Session ou email du professeur 1 non trouvé." });
            await SendProfSignatureMail(session, 1);
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
            if (session == null || string.IsNullOrEmpty(session.ProfEmail2))
                return NotFound(new { error = true, message = "Session ou email du professeur 2 non trouvé." });
            await SendProfSignatureMail(session, 2);
            return Ok(new { message = "Mail renvoyé au professeur 2." });
        }

        /**
        * ImportIcs
        *
        * This method imports ICS data from a URL and processes it.
        */
        [HttpPost("import-ics")]
        public async Task<IActionResult> ImportIcs([FromBody] ImportIcsModel model)
        {
            _logger.LogInformation($"=== DÉBUT ImportIcs pour l'année {model.Year} à {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

            if (string.IsNullOrWhiteSpace(model.IcsUrl) || string.IsNullOrWhiteSpace(model.Year))
                return BadRequest(new { error = true, message = "URL ICS ou année manquante." });

            try
            {
                var icsContent = await FetchIcsContent(model.IcsUrl);
                var calendar = Ical.Net.Calendar.Load(icsContent);

                var eventsByTimeSlot = await ParseIcsEvents(calendar.Events);
                _logger.LogInformation($"Nombre d'événements parsés : {eventsByTimeSlot.Count}");

                var existingSessions = await _context.Sessions
                    .Where(s => s.Year == model.Year)
                    .ToListAsync();
                _logger.LogInformation($"Nombre de sessions existantes pour {model.Year} : {existingSessions.Count} (dont {existingSessions.Count(s => s.IsMerged)} fusionnées)");

                var importedSessions = new HashSet<(DateTime, TimeSpan, TimeSpan)>();

                await ProcessTimeSlots(eventsByTimeSlot, model.Year, existingSessions, importedSessions);
                await CleanupOldSessions(existingSessions, importedSessions);

                _logger.LogInformation($"=== Début fusion des sessions pour {model.Year} ===");
                var mergeResult = await MergeSameSessions(model.Year);
                _logger.LogInformation($"=== FIN ImportIcs pour {model.Year} à {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

                return mergeResult;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'import ICS : {ex.Message}");
                return StatusCode(500, new { error = true, message = "Erreur lors de l'import ICS." });
            }
        }

        /**
         * FetchIcsContent
         *
         * This method fetches the ICS content from a given URL.
         */
        private async Task<string> FetchIcsContent(string icsUrl)
        {
            using var httpClient = new HttpClient();
            return await httpClient.GetStringAsync(icsUrl);
        }

        private async Task<Dictionary<(DateTime date, TimeSpan startTime, TimeSpan endTime), List<EventInfo>>>
            ParseIcsEvents(IUniqueComponentList<CalendarEvent> events)
        {
            var frenchTimeZone = GetFrenchTimeZone();
            var eventsByTimeSlot = new Dictionary<(DateTime, TimeSpan, TimeSpan), List<EventInfo>>();

            foreach (var icsEvent in events)
            {
                var eventInfo = ParseSingleEvent(icsEvent, frenchTimeZone);

                if (eventInfo == null || eventInfo.Date < DateTime.Today)
                    continue;

                var timeSlot = (eventInfo.Date, eventInfo.StartTime, eventInfo.EndTime);

                if (!eventsByTimeSlot.ContainsKey(timeSlot))
                    eventsByTimeSlot[timeSlot] = new List<EventInfo>();

                eventsByTimeSlot[timeSlot].Add(eventInfo);
            }

            return eventsByTimeSlot;
        }


        /**
        * ParseSingleEvent
        *
        * This method parses a single ICS event into an EventInfo object.
        */
        private EventInfo ParseSingleEvent(CalendarEvent icsEvent, TimeZoneInfo frenchTimeZone)
        {
            var startDateLocal = TimeZoneInfo.ConvertTimeFromUtc(icsEvent.Start.AsUtc, frenchTimeZone);
            var endDateLocal = TimeZoneInfo.ConvertTimeFromUtc(icsEvent.End.AsUtc, frenchTimeZone);

            var eventInfo = new EventInfo
            {
                Date = startDateLocal.Date,
                StartTime = startDateLocal.TimeOfDay,
                EndTime = endDateLocal.TimeOfDay,
                Summary = icsEvent.Summary ?? "Session importée",
                Room = icsEvent.Location ?? string.Empty
            };

            if (!eventInfo.Summary.Contains("travail personnel", StringComparison.OrdinalIgnoreCase))
            {
                ExtractProfessorInfo(icsEvent.Description, eventInfo);
            }

            return eventInfo;
        }

        /**
        * ExtractProfessorInfo
        *   
        * This method extracts professor information from the event description.
        */
        private void ExtractProfessorInfo(string description, EventInfo eventInfo)
        {
            if (string.IsNullOrWhiteSpace(description))
                return;

            var descLines = description
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var profLine = descLines.LastOrDefault(l => !l.StartsWith("(Exporté le:"));

            if (string.IsNullOrWhiteSpace(profLine))
                return;

            var parts = profLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 1)
                eventInfo.ProfName = parts[0];

            if (parts.Length >= 2)
                eventInfo.ProfFirstname = parts[1];
        }

        private TimeZoneInfo GetFrenchTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris")
                ?? TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }

        /**
        * ProcessTimeSlots
        *
        * This method processes each time slot and updates or creates sessions accordingly.
        */
        private async Task ProcessTimeSlots(
            Dictionary<(DateTime date, TimeSpan startTime, TimeSpan endTime), List<EventInfo>> eventsByTimeSlot,
            string year,
            List<Session> existingSessions,
            HashSet<(DateTime, TimeSpan, TimeSpan)> importedSessions)
        {
            foreach (var (timeSlot, eventInfos) in eventsByTimeSlot)
            {
                var (date, startTime, endTime) = timeSlot;
                importedSessions.Add(timeSlot);

                var exactMatch = await FindExactMatch(date, startTime, endTime, year);
                var mergedMatches = FindMergedMatches(existingSessions, date, startTime, endTime, year);

                if (mergedMatches.Any())
                {
                    foreach (var mergedSession in mergedMatches)
                    {
                        importedSessions.Add((mergedSession.Date, mergedSession.StartTime, mergedSession.EndTime));
                    }

                    // PROTECTION : Si une session fusionnée couvre déjà cette plage, on ne fait RIEN
                    _logger.LogInformation(
                        $"Session fusionnée ID {mergedMatches.First().Id} couvre déjà " +
                        $"la plage {date:yyyy-MM-dd} {startTime}-{endTime}. Aucune action nécessaire.");
                    continue;
                }

                if (await HandleExistingSession(exactMatch, mergedMatches, eventInfos))
                    continue;

                await RemoveOverlappingSessions(existingSessions, date, startTime, endTime, year);
                await CreateNewSession(timeSlot, eventInfos, year);
            }

            await _context.SaveChangesAsync();
        }

        /**
        * FindExactMatch
        *
        * This method finds an exact matching session based on date, start time, end time, and year.
        */
        private async Task<Session?> FindExactMatch(DateTime date, TimeSpan startTime, TimeSpan endTime, string year)
        {
            return await _context.Sessions.FirstOrDefaultAsync(s =>
                s.Date == date &&
                s.StartTime == startTime &&
                s.EndTime == endTime &&
                s.Year == year);
        }

        /**
        * FindMergedMatches
        *
        * This method finds merged sessions that cover the specified time slot.
        */
        private List<Session> FindMergedMatches(
            List<Session> existingSessions,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            string year)
        {
            return existingSessions
                .Where(s => s.Date == date &&
                            s.Year == year &&
                            s.IsMerged &&
                            s.StartTime <= startTime &&
                            s.EndTime >= endTime)
                .ToList();
        }

        /**
        * HandleExistingSession
        * This method handles existing sessions based on exact and merged matches.
        */
        private async Task<bool> HandleExistingSession(
            Session? exactMatch,
            List<Session> mergedMatches,
            List<EventInfo> eventInfos)
        {
            if (exactMatch != null)
            {
                if (exactMatch.IsMerged)
                {
                    await UpdateMergedSessionIfNeeded(exactMatch, eventInfos);
                    return true;
                }
                else
                {
                    return await HandleNonMergedExactMatch(exactMatch, eventInfos);
                }
            }

            if (mergedMatches.Any())
            {
                var existingSession = mergedMatches.First();
                _logger.LogInformation(
                    $"La session fusionnée existante ID {existingSession.Id} " +
                    $"({existingSession.Date}, {existingSession.StartTime}-{existingSession.EndTime}) " +
                    $"couvre déjà cette plage horaire");
                return true;
            }

            return false;
        }

        /**
        * UpdateMergedSessionIfNeeded
        *
        * This method updates a merged session if its information differs from the merged event info.
        */
        private async Task UpdateMergedSessionIfNeeded(Session exactMatch, List<EventInfo> eventInfos)
        {
            var mergedInfo = MergeEventInfos(eventInfos);

            bool needsUpdate = exactMatch.Name != mergedInfo.MergedName ||
                            exactMatch.ProfName != mergedInfo.ProfName ||
                            exactMatch.ProfFirstname != mergedInfo.ProfFirstname ||
                            exactMatch.Room != mergedInfo.Room ||
                            exactMatch.ProfName2 != mergedInfo.ProfName2 ||
                            exactMatch.ProfFirstname2 != mergedInfo.ProfFirstname2;

            if (needsUpdate)
            {
                _logger.LogInformation(
                    $"Mise à jour des informations de la session fusionnée ID {exactMatch.Id} " +
                    $"({exactMatch.Date}, {exactMatch.StartTime}-{exactMatch.EndTime})");

                exactMatch.Name = mergedInfo.MergedName;
                exactMatch.ProfName = mergedInfo.ProfName;
                exactMatch.ProfFirstname = mergedInfo.ProfFirstname;
                exactMatch.Room = mergedInfo.Room;
                exactMatch.ProfName2 = mergedInfo.ProfName2;
                exactMatch.ProfFirstname2 = mergedInfo.ProfFirstname2;

                _context.Sessions.Update(exactMatch);
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogInformation(
                    $"Préservation de la session fusionnée ID {exactMatch.Id} " +
                    "(aucune modification nécessaire)");
            }
        }

        /**
        * HandleNonMergedExactMatch
        * This method handles a non-merged exact match session.
        */
        private async Task<bool> HandleNonMergedExactMatch(Session exactMatch, List<EventInfo> eventInfos)
        {
            var mergedInfo = MergeEventInfos(eventInfos);

            if (exactMatch.Name != mergedInfo.MergedName ||
                exactMatch.ProfName != mergedInfo.ProfName ||
                exactMatch.ProfFirstname != mergedInfo.ProfFirstname ||
                exactMatch.Room != mergedInfo.Room)
            {
                await DeleteSessionWithAttendances(exactMatch);
                return false;
            }

            return true;
        }

        /**
        * RemoveOverlappingSessions
        * This method removes sessions that overlap with the specified time slot.
        */
        private async Task RemoveOverlappingSessions(
            List<Session> existingSessions,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            string year)
        {
            var allSessionsSameDay = await _context.Sessions
                .Where(s => s.Year == year && s.Date == date)
                .ToListAsync();

            var overlappingSessions = allSessionsSameDay
                .Where(s => s.StartTime < endTime &&
                            s.EndTime > startTime &&
                            !s.IsMerged)
                .ToList();

            foreach (var overlap in overlappingSessions)
            {
                // PROTECTION : Ne pas supprimer une session avec des étudiants présents
                var hasPresent = await _context.Attendances
                    .AnyAsync(a => a.SessionId == overlap.Id && a.Status == AttendanceStatus.Present);

                if (hasPresent)
                {
                    _logger.LogInformation(
                        $"Conservation de la session qui se chevauche ID {overlap.Id} " +
                        $"({overlap.Date}, {overlap.StartTime}-{overlap.EndTime}) - Contient des étudiants présents");
                    continue;
                }

                _logger.LogInformation(
                    $"Suppression de la session qui se chevauche ID {overlap.Id} " +
                    $"({overlap.Date}, {overlap.StartTime}-{overlap.EndTime})");

                await DeleteSessionWithAttendances(overlap);
            }
        }
        /**
        * CreateNewSession
        * This method creates a new session and associated attendances.
        */
        private async Task CreateNewSession(
            (DateTime date, TimeSpan startTime, TimeSpan endTime) timeSlot,
            List<EventInfo> eventInfos,
            string year)
        {
            var (date, startTime, endTime) = timeSlot;
            var mergedInfo = MergeEventInfos(eventInfos);

            var session = new Session
            {
                Date = date,
                StartTime = startTime,
                EndTime = endTime,
                Year = year,
                Name = mergedInfo.MergedName,
                ProfName = mergedInfo.ProfName,
                ProfFirstname = mergedInfo.ProfFirstname,
                Room = mergedInfo.Room,
                ProfName2 = mergedInfo.ProfName2,
                ProfFirstname2 = mergedInfo.ProfFirstname2,
                ProfSignatureToken = Guid.NewGuid().ToString(),
                ProfSignatureToken2 = !string.IsNullOrEmpty(mergedInfo.ProfName2)
                    ? Guid.NewGuid().ToString()
                    : null,
                ValidationCode = new Random().Next(1000, 9999).ToString(),
                IsMerged = eventInfos.Count > 1
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            await CreateAttendancesForSession(session, year);
        }

        /**
        * CreateAttendancesForSession
        * This method creates attendance records for all students in the specified year for a session.
        */
        private async Task CreateAttendancesForSession(Session session, string year)
        {
            var students = await _context.Users
                .Where(u => u.Year == year)
                .ToListAsync();

            foreach (var student in students)
            {
                var attendance = new Attendance
                {
                    SessionId = session.Id,
                    StudentId = student.Id,
                    Status = AttendanceStatus.Absent,
                    Comment = string.Empty
                };
                _context.Attendances.Add(attendance);
            }
        }

        /**
        * CleanupOldSessions
        * This method cleans up old sessions that are no longer present in the imported data.
        */
        private async Task CleanupOldSessions(
            List<Session> existingSessions,
            HashSet<(DateTime, TimeSpan, TimeSpan)> importedSessions)
        {
            // Récupérer toutes les sessions fusionnées de l'année pour cette vérification
            var mergedSessions = existingSessions.Where(s => s.IsMerged).ToList();

            foreach (var oldSession in existingSessions)
            {
                // PROTECTION ABSOLUE : Ne jamais supprimer une session fusionnée
                if (oldSession.IsMerged)
                {
                    _logger.LogInformation(
                        $"Préservation obligatoire de la session fusionnée ID {oldSession.Id} " +
                        $"({oldSession.Date}, {oldSession.StartTime}-{oldSession.EndTime})");
                    continue;
                }

                if (ShouldDeleteSession(oldSession, importedSessions, existingSessions, mergedSessions))
                {
                    _logger.LogInformation(
                        $"Suppression de l'ancienne session ID {oldSession.Id} " +
                        $"({oldSession.Date}, {oldSession.StartTime}-{oldSession.EndTime})");

                    await DeleteSessionWithAttendances(oldSession);
                }
            }
        }

        /**
        * ShouldDeleteSession
        * This method determines whether a session should be deleted based on various criteria.
        */
        private bool ShouldDeleteSession(
            Session session,
            HashSet<(DateTime, TimeSpan, TimeSpan)> importedSessions,
            List<Session> existingSessions,
            List<Session> mergedSessions)
        {
            if (session.Date < DateTime.Today)
                return false;

            // PROTECTION : Ne jamais supprimer les sessions fusionnées, même futures
            if (session.IsMerged)
            {
                _logger.LogInformation(
                    $"Conservation de la session fusionnée ID {session.Id} " +
                    $"({session.Date}, {session.StartTime}-{session.EndTime}) - Protection active");
                return false;
            }

            // PROTECTION : Ne jamais supprimer une session avec des étudiants présents
            var hasPresent = _context.Attendances
                .Any(a => a.SessionId == session.Id && a.Status == AttendanceStatus.Present);

            if (hasPresent)
            {
                _logger.LogInformation(
                    $"Conservation de la session ID {session.Id} " +
                    $"({session.Date}, {session.StartTime}-{session.EndTime}) - Contient des étudiants présents");
                return false;
            }

            if (importedSessions.Contains((session.Date, session.StartTime, session.EndTime)))
                return false;

            // Vérifier si cette session fait partie d'une session fusionnée
            bool isPartOfMergedSession = mergedSessions.Any(merged =>
                merged.Date == session.Date &&
                merged.StartTime <= session.StartTime &&
                merged.EndTime >= session.EndTime &&
                merged.Name == session.Name &&
                merged.Room == session.Room &&
                merged.ProfName == session.ProfName &&
                merged.ProfFirstname == session.ProfFirstname);

            if (isPartOfMergedSession)
            {
                _logger.LogInformation(
                    $"Conservation de la session ID {session.Id} " +
                    "car elle fait partie d'une session fusionnée avec les mêmes informations");
                return false;
            }

            bool coveredByMergedSession = existingSessions.Any(s =>
                s.IsMerged &&
                s.Date == session.Date &&
                s.StartTime <= session.StartTime &&
                s.EndTime >= session.EndTime);

            if (coveredByMergedSession)
            {
                _logger.LogInformation(
                    $"Conservation de la session ID {session.Id} " +
                    "car elle est englobée par une session fusionnée");
                return false;
            }

            return true;
        }

        /**
        * DeleteSessionWithAttendances
        * This method deletes a session along with all its associated attendance records.
        */
        private async Task DeleteSessionWithAttendances(Session session)
        {
            var attendances = _context.Attendances.Where(a => a.SessionId == session.Id);
            _context.Attendances.RemoveRange(attendances);
            await _context.SaveChangesAsync();

            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();
        }

        /**
        * MergeEventInfos
        * This method merges multiple EventInfo objects into a single MergedEventInfo.
        */
        private MergedEventInfo MergeEventInfos(List<EventInfo> eventInfos)
        {
            var primaryEvent = eventInfos.First();

            if (eventInfos.Count == 1)
            {
                return new MergedEventInfo
                {
                    MergedName = primaryEvent.Summary,
                    Room = primaryEvent.Room,
                    ProfName = primaryEvent.ProfName,
                    ProfFirstname = primaryEvent.ProfFirstname
                };
            }

            _logger.LogInformation(
                $"Fusion de {eventInfos.Count} événements pour le créneau " +
                $"{primaryEvent.Date} {primaryEvent.StartTime}-{primaryEvent.EndTime}");

            var mergedName = string.Join(" / ", eventInfos.Select(e => e.Summary).Distinct());
            var mergedRoom = string.Join(" / ",
                eventInfos.Select(e => e.Room).Where(r => !string.IsNullOrEmpty(r)).Distinct());

            var firstProf = eventInfos.FirstOrDefault(e => !string.IsNullOrEmpty(e.ProfName));
            var secondProf = eventInfos
                .Skip(1)
                .FirstOrDefault(e => !string.IsNullOrEmpty(e.ProfName) &&
                                    (e.ProfName != firstProf?.ProfName ||
                                    e.ProfFirstname != firstProf?.ProfFirstname));

            return new MergedEventInfo
            {
                MergedName = mergedName,
                Room = mergedRoom,
                ProfName = firstProf?.ProfName ?? string.Empty,
                ProfFirstname = firstProf?.ProfFirstname ?? string.Empty,
                ProfName2 = secondProf?.ProfName ?? string.Empty,
                ProfFirstname2 = secondProf?.ProfFirstname ?? string.Empty
            };
        }


        private class EventInfo
        {
            public DateTime Date { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string Summary { get; set; } = string.Empty;
            public string Room { get; set; } = string.Empty;
            public string ProfName { get; set; } = string.Empty;
            public string ProfFirstname { get; set; } = string.Empty;
        }

        private class MergedEventInfo
        {
            public string MergedName { get; set; } = string.Empty;
            public string Room { get; set; } = string.Empty;
            public string ProfName { get; set; } = string.Empty;
            public string ProfFirstname { get; set; } = string.Empty;
            public string ProfName2 { get; set; } = string.Empty;
            public string ProfFirstname2 { get; set; } = string.Empty;
        }
        /**
         * MergeSameSessions
         *
         * This method merges sessions that are the same.
         */
        public async Task<IActionResult> MergeSameSessions(string year)
        {
            try
            {
                var sessions = await _context.Sessions
                    .Where(s => s.Year == year)
                    .ToListAsync();

                sessions = sessions
                    .OrderBy(s => s.Date)
                    .ThenBy(s => s.StartTime.Hours * 60 + s.StartTime.Minutes) // Convertir en minutes pour le tri
                    .ToList();

                _logger.LogInformation($"Début de la fusion des sessions pour l'année {year}. {sessions.Count} sessions trouvées.");

                bool anyChanges = false;

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var sessionDict = sessions.ToDictionary(s => s.Id);

                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];

                        if (!sessionDict.ContainsKey(session.Id))
                        {
                            _logger.LogDebug($"Session {session.Id} déjà traitée ou supprimée, ignorée.");
                            continue;
                        }

                        var candidateSessions = sessions
                            .Take(i)
                            .Where(s => s.Date == session.Date &&
                                  sessionDict.ContainsKey(s.Id) &&
                                  s.EndTime == session.StartTime - TimeSpan.FromMinutes(15) &&
                                  s.Name == session.Name &&
                                  s.Room == session.Room &&
                                  s.ProfName == session.ProfName &&
                                  s.ProfFirstname == session.ProfFirstname)
                            .ToList();

                        if (candidateSessions.Count == 0)
                        {
                            _logger.LogDebug($"Aucune session candidate trouvée pour fusionner avec la session {session.Id}");
                            continue;
                        }

                        var sessionBefore = candidateSessions
                            .AsEnumerable()
                            .OrderByDescending(s => s.EndTime)
                            .First();

                        _logger.LogInformation($"Fusion des sessions: ID {sessionBefore.Id} ({sessionBefore.StartTime}-{sessionBefore.EndTime}) et ID {session.Id} ({session.StartTime}-{session.EndTime})");

                        sessionBefore.EndTime = session.EndTime;
                        sessionBefore.IsMerged = true; // Marquer la session comme fusionnée
                        _context.Sessions.Update(sessionBefore);

                        // Récupérer et traiter les présences
                        var attendances = await _context.Attendances
                            .Where(a => a.SessionId == session.Id)
                            .ToListAsync();

                        _logger.LogDebug($"Traitement de {attendances.Count} présences pour la session {session.Id}");

                        foreach (var attendance in attendances)
                        {
                            var existingAttendance = await _context.Attendances
                                .FirstOrDefaultAsync(a => a.SessionId == sessionBefore.Id &&
                                                         a.StudentId == attendance.StudentId);

                            if (existingAttendance == null)
                            {
                                attendance.SessionId = sessionBefore.Id;
                                _logger.LogDebug($"Présence de l'étudiant {attendance.StudentId} transférée à la session {sessionBefore.Id}");
                            }
                            else
                            {
                                if (attendance.Status == AttendanceStatus.Present)
                                {
                                    existingAttendance.Status = AttendanceStatus.Present;
                                    _logger.LogDebug($"Statut de présence de l'étudiant {attendance.StudentId} mis à jour à 'Present'");
                                }

                                if (!string.IsNullOrEmpty(attendance.Comment) && string.IsNullOrEmpty(existingAttendance.Comment))
                                {
                                    existingAttendance.Comment = attendance.Comment;
                                    _logger.LogDebug($"Commentaire de présence de l'étudiant {attendance.StudentId} mis à jour");
                                }

                                _context.Attendances.Remove(attendance);
                            }
                        }

                        _context.Sessions.Remove(session);

                        sessionDict.Remove(session.Id);

                        anyChanges = true;

                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    if (anyChanges)
                    {
                        _logger.LogInformation($"Sessions fusionnées avec succès pour l'année {year}.");
                    }
                    else
                    {
                        _logger.LogInformation($"Aucune fusion de sessions nécessaire pour l'année {year}.");
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Erreur lors de la fusion des sessions pour l'année {year}: {ex.Message}");
                    throw;
                }

                return Ok(new { message = "Sessions fusionnées avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception globale lors de la fusion des sessions: {ex.Message}");
                return StatusCode(500, new { error = true, message = $"Erreur lors de la fusion des sessions: {ex.Message}" });
            }
        }

        /**
         * ImportIcsModel
         *
         * This model is used for importing ICS data.
         */
        public class ImportIcsModel
        {
            public string IcsUrl { get; set; } = string.Empty;
            public string Year { get; set; } = string.Empty;
            public string? ProfName { get; set; }
            public string? ProfFirstname { get; set; }
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
                profEmail = session.ProfEmail;
                profSignatureToken = session.ProfSignatureToken;
                profName = $"{session.ProfFirstname} {session.ProfName}";
                session.IsMailSent = true;
            }
            else if (professorNumber == 2)
            {
                profEmail = session.ProfEmail2;
                profSignatureToken = session.ProfSignatureToken2;
                profName = $"{session.ProfFirstname2} {session.ProfName2}";
                session.IsMailSent2 = true;
            }
            else
            {
                throw new ArgumentException("Le numéro de professeur doit être 1 ou 2", nameof(professorNumber));
            }

            await _context.SaveChangesAsync();

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
- Heure de début : {session.StartTime.ToString(@"hh\:mm")} - Heure de fin : {session.EndTime.ToString(@"hh\:mm")}
- Validation : {session.ValidationCode}

Cordialement";

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

                var fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL");
                if (string.IsNullOrWhiteSpace(fromEmail))
                    throw new Exception("L'adresse email d'expéditeur (SMTP_FROM_EMAIL) n'est pas définie dans les variables d'environnement.");
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false,
                };
                mailMessage.To.Add(profEmail);
                mailMessage.Headers.Add("X-Priority", "1");

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'envoi du mail au prof : {ex.Message}");
            }
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
                if (profTokenValue != session.ProfSignatureToken)
                {
                    var sessionByToken = await _context.Sessions
                        .FirstOrDefaultAsync(s => s.ProfSignatureToken == profTokenValue);

                    if (sessionByToken != null)
                    {
                        session = sessionByToken;
                        sessionId = session.Id;
                        _logger.LogInformation($"Session trouvée par token: {sessionId}");
                    }
                }

                if (profTokenValue == session.ProfSignatureToken)
                {
                    isAuthorized = true;
                    _logger.LogInformation($"Accès autorisé par token professeur pour modifier le statut de présence de {studentNumber} dans la session {sessionId}");
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
                nextMail = mailTime
            });
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

            if (!string.IsNullOrEmpty(profTokenValue) && profTokenValue != sessionNormal.ProfSignatureToken)
            {
                var sessionByToken = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.ProfSignatureToken == profTokenValue);

                if (sessionByToken != null)
                {
                    sessionNormal = sessionByToken;
                    sessionId = sessionNormal.Id;
                    _logger.LogInformation($"Session trouvée par token: {sessionId}");
                }
            }

            if (!string.IsNullOrEmpty(profTokenValue) && profTokenValue == sessionNormal.ProfSignatureToken)
            {
                isAuthorized = true;
                accessType = "prof-token";
                _logger.LogInformation($"Accès autorisé par token professeur pour modifier le commentaire de présence de {studentNumber} dans la session {sessionId}");
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