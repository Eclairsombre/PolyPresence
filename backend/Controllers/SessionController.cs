using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net;

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
            var icsLinks = await context.IcsLinks.ToListAsync();
            foreach (var link in icsLinks)
            {
                try
                {
                    var importModel = new ImportIcsModel { IcsUrl = link.Url, Year = link.Year };
                    var controller = new SessionController(context, (ILogger<SessionController>)logger, _serviceScopeFactory);
                    await controller.ImportIcs(importModel);
                }
                catch (Exception ex)
                {
                    logger.LogError($"Erreur lors de l'import ICS pour {link.Url} : {ex.Message}");
                }

            }
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
                _logger.LogInformation($"Vérification des sessions à {now} pour l'envoi de mails au professeur.");
                var sessions = await scopedContext.Sessions
                    .Where(s => !s.IsMailSent && s.Date == now.Date && s.ProfEmail != "")
                    .ToListAsync();
                sessions = sessions
                    .Where(s => s.StartTime <= now.TimeOfDay)
                    .ToList();
                foreach (var session in sessions)
                {
                    try
                    {
                        await SendProfSignatureMail(session);
                        session.IsMailSent = true;
                        await scopedContext.SaveChangesAsync();
                        logger.LogInformation($"Mail envoyé automatiquement au professeur pour la session {session.Id}.");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Erreur lors de l'envoi automatique du mail pour la session {session.Id} : {ex.Message}");
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
                    s.IsSent,
                    s.IsMailSent
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
                    session.IsSent,
                    session.IsMailSent
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
                    s.IsSent,
                    s.IsMailSent
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
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.ProfSignatureToken == token);
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
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.ProfSignatureToken == token);
            if (session == null)
            {
                return NotFound(new { error = true, message = "Session non trouvée pour ce lien." });
            }
            session.ProfSignature = signatureData.Signature;
            if (!string.IsNullOrWhiteSpace(signatureData.Name))
                session.ProfName = signatureData.Name;
            if (!string.IsNullOrWhiteSpace(signatureData.Firstname))
                session.ProfFirstname = signatureData.Firstname;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Signature du professeur enregistrée avec succès." });
        }

        /**
         * PutSession
         *
         * This method updates an existing session in the database.
         */
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSession(int id, Session session)
        {
            if (id != session.Id)
            {
                return BadRequest();
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
         */
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
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
                    currentSession.ProfSignature,
                    currentSession.ProfSignatureToken,
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
        [Authorize]
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

            var isAdmin = false;
            var isDelegate = false;

            if (User.Identity?.IsAuthenticated == true)
            {
                var isAdminClaim = User.FindFirstValue("role");
                var isDelegateClaim = User.FindFirstValue("isDelegate");

                isAdmin = isAdminClaim == "Admin";
                isDelegate = isDelegateClaim == "true";

                var userStudentNumber = User.FindFirstValue("studentNumber");
                _logger.LogInformation($"User {userStudentNumber} requesting attendance info - Role: {isAdminClaim}, IsDelegate: {isDelegateClaim}");
                _logger.LogInformation($"Interpreted values - IsAdmin: {isAdmin}, IsDelegate: {isDelegate}");
            }
            else
            {
                _logger.LogInformation("No authenticated user found for attendance request");
            }

            var userDto = new
            {
                user.Id,
                user.Name,
                user.Firstname,
                user.StudentNumber,
                user.Email,
                user.Year,
                user.Signature,
                user.IsAdmin,
                user.IsDelegate
            };

            if (!isAdmin && !isDelegate)
            {
                _logger.LogInformation("Hiding validation code in attendance response");

                var sessionDto = new
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
                    session.IsSent,
                    session.IsMailSent
                };

                var attendanceDto = new
                {
                    attendance.Id,
                    attendance.SessionId,
                    Session = sessionDto,
                    attendance.StudentId,
                    User = userDto,
                    attendance.Status,
                    attendance.Comment
                };

                return Ok(attendanceDto);
            }
            else
            {
                _logger.LogInformation("Returning attendance with validation code");

                var sessionDto = new
                {
                    session.Id,
                    session.Date,
                    session.StartTime,
                    session.EndTime,
                    session.Year,
                    session.Name,
                    session.Room,
                    session.ValidationCode,  // Inclus pour admin/délégué
                    session.ProfName,
                    session.ProfFirstname,
                    session.ProfEmail,
                    session.ProfSignature,
                    session.ProfSignatureToken,
                    session.IsSent,
                    session.IsMailSent
                };

                var attendanceDto = new
                {
                    attendance.Id,
                    attendance.SessionId,
                    Session = sessionDto,
                    attendance.StudentId,
                    User = userDto,
                    attendance.Status,
                    attendance.Comment
                };

                return Ok(attendanceDto);
            }
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
                    s.IsSent,
                    s.IsMailSent
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
            return Ok(new { message = "Email du professeur enregistré et mail envoyé." });
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
                return NotFound(new { error = true, message = "Session ou email du professeur non trouvé." });
            await SendProfSignatureMail(session);
            return Ok(new { message = "Mail renvoyé au professeur." });
        }

        /**
         * ImportIcs
         *
         * This method imports ICS data from a URL and processes it.
         */
        [HttpPost("import-ics")]
        public async Task<IActionResult> ImportIcs([FromBody] ImportIcsModel model)
        {
            if (string.IsNullOrWhiteSpace(model.IcsUrl) || string.IsNullOrWhiteSpace(model.Year))
                return BadRequest(new { error = true, message = "URL ICS ou année manquante." });

            try
            {
                using var httpClient = new HttpClient();
                var icsContent = await httpClient.GetStringAsync(model.IcsUrl);
                var calendar = Ical.Net.Calendar.Load(icsContent);
                var events = calendar.Events;
                int createdCount = 0;
                var existingSessions = await _context.Sessions.Where(s => s.Year == model.Year).ToListAsync();
                var importedSessions = new HashSet<(DateTime, TimeSpan, TimeSpan)>();
                foreach (var evt in events)
                {
                    var frenchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris") ??
                                         TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

                    var startDateUtc = evt.Start.AsUtc;
                    var endDateUtc = evt.End.AsUtc;

                    var startDateLocal = TimeZoneInfo.ConvertTimeFromUtc(startDateUtc, frenchTimeZone);
                    var endDateLocal = TimeZoneInfo.ConvertTimeFromUtc(endDateUtc, frenchTimeZone);

                    var date = startDateLocal.Date;
                    var startTime = startDateLocal.TimeOfDay;
                    var endTime = endDateLocal.TimeOfDay;
                    var summary = evt.Summary ?? "Session importée";
                    var year = model.Year;

                    string room = evt.Location ?? string.Empty;

                    string profName = string.Empty;
                    string profFirstname = string.Empty;

                    if (date < DateTime.Today)
                        continue;
                    if (summary.Contains("travail personnel", StringComparison.OrdinalIgnoreCase))
                    {
                        profName = string.Empty;
                        profFirstname = string.Empty;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(evt.Description))
                        {
                            var descLines = evt.Description.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                            var profLine = descLines.LastOrDefault(l => !l.StartsWith("(Exporté le:"));
                            if (!string.IsNullOrWhiteSpace(profLine))
                            {
                                var parts = profLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length == 2)
                                {
                                    profName = parts[0];
                                    profFirstname = parts[1];
                                }
                                else if (parts.Length == 1)
                                {
                                    profName = parts[0];
                                }
                            }
                        }
                    }

                    importedSessions.Add((date, startTime, endTime));

                    var existingSession = await _context.Sessions.FirstOrDefaultAsync(s => s.Date == date && s.StartTime == startTime && s.EndTime == endTime && s.Year == year);
                    if (existingSession != null)
                    {
                        if (existingSession.Name != summary || existingSession.ProfName != profName || existingSession.ProfFirstname != profFirstname || existingSession.Room != room)
                        {
                            var oldAttendances = _context.Attendances.Where(a => a.SessionId == existingSession.Id);
                            _context.Attendances.RemoveRange(oldAttendances);
                            await _context.SaveChangesAsync();
                            _context.Sessions.Remove(existingSession);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    var allSessionsSameDay = await _context.Sessions
                        .Where(s => s.Year == year && s.Date == date)
                        .ToListAsync();
                    var overlappingSessions = allSessionsSameDay
                        .Where(s => s.StartTime < endTime && s.EndTime > startTime)
                        .ToList();
                    foreach (var overlap in overlappingSessions)
                    {
                        var attendances = _context.Attendances.Where(a => a.SessionId == overlap.Id);
                        _context.Attendances.RemoveRange(attendances);
                        await _context.SaveChangesAsync();
                        _context.Sessions.Remove(overlap);
                        await _context.SaveChangesAsync();
                    }

                    var session = new Session
                    {
                        Date = date,
                        StartTime = startTime,
                        EndTime = endTime,
                        Year = year,
                        Name = summary,
                        ProfName = profName,
                        ProfFirstname = profFirstname,
                        Room = room,
                        ProfSignatureToken = Guid.NewGuid().ToString(),
                        ValidationCode = new Random().Next(1000, 9999).ToString(),
                    };
                    _context.Sessions.Add(session);
                    await _context.SaveChangesAsync();

                    var sessionId = session.Id;
                    var students = await _context.Users.Where(u => u.Year == year).ToListAsync();
                    foreach (var student in students)
                    {
                        var attendance = new Attendance
                        {
                            SessionId = sessionId,
                            StudentId = student.Id,
                            Status = AttendanceStatus.Absent,
                            Comment = string.Empty
                        };
                        _context.Attendances.Add(attendance);
                    }
                    createdCount++;
                }
                await _context.SaveChangesAsync();
                foreach (var oldSession in existingSessions)
                {
                    if (oldSession.Date >= DateTime.Today && !importedSessions.Contains((oldSession.Date, oldSession.StartTime, oldSession.EndTime)))
                    {
                        var attendances = _context.Attendances.Where(a => a.SessionId == oldSession.Id);
                        _context.Attendances.RemoveRange(attendances);
                        await _context.SaveChangesAsync();
                        _context.Sessions.Remove(oldSession);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'import ICS : {ex.Message}");
                return StatusCode(500, new { error = true, message = "Erreur lors de l'import ICS." });
            }

            return await MergeSameSessions(model.Year);

        }
        /**
         * MergeSameSessions
         *
         * This method merges sessions that are the same.
         */
        public async Task<IActionResult> MergeSameSessions(string year)
        {
            var sessions = await _context.Sessions
                .Where(s => s.Year == year)
                .ToListAsync();

            foreach (var session in sessions.ToList())
            {
                var candidateSessions = sessions
                    .Where(s => s.Date == session.Date && s.StartTime < session.StartTime)
                    .ToList();

                var sessionBefore = candidateSessions
                    .FirstOrDefault(s => s.EndTime == session.StartTime - TimeSpan.FromMinutes(15));

                if (sessionBefore != null && session.Name == sessionBefore.Name && session.Room == sessionBefore.Room)
                {
                    sessionBefore.EndTime = session.EndTime;

                    var attendances = _context.Attendances.Where(a => a.SessionId == session.Id);
                    _context.Attendances.RemoveRange(attendances);

                    _context.Sessions.Remove(session);
                }
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Sessions fusionnées avec succès." });
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
            if (session == null || string.IsNullOrWhiteSpace(session.ProfEmail) || string.IsNullOrWhiteSpace(session.ProfSignatureToken))
                return;

            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
            var link = $"{frontendUrl}/prof-signature/{session.ProfSignatureToken}";
            var subject = "Signature de la feuille de présence";
            var body = $@"Bonjour,

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
                mailMessage.To.Add(session.ProfEmail);
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
         */
        [HttpPost("{sessionId}/attendance-status/{studentNumber}")]
        public async Task<IActionResult> ChangeAttendanceStatus(int sessionId, string studentNumber, [FromBody] ChangeAttendanceStatusModel model)
        {
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
            var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == user.Id);
            if (attendance == null)
            {
                return NotFound(new { error = true, message = "Aucune présence trouvée pour cette session et cet étudiant." });
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
            var mailRemaining = mailTime - now;
            return Ok(new
            {
                nextImport = importTime,
                importRemaining = importRemaining.ToString(@"hh\:mm\:ss"),
                nextMail = mailTime,
                mailRemaining = mailRemaining.ToString(@"hh\:mm\:ss")
            });
        }


        /**
         * UpdateAttendanceComment
         *
         * This method updates the comment for a student's attendance in a session.
         */
        [HttpPost("{sessionId}/attendance-comment/{studentNumber}")]
        public async Task<IActionResult> UpdateAttendanceComment(int sessionId, string studentNumber, [FromBody] CommentUpdateModel model)
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

            attendance.Comment = model.Comment;
            await _context.SaveChangesAsync();

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
        }
    }
}