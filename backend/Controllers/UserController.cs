using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;

namespace backend.Controllers
{
    /**
     * UserController
     *
     * This controller handles CRUD operations for User entities.
     */
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionController> _logger;

        public UserController(ApplicationDbContext context, ILogger<SessionController> logger)
        {
            _context = context;
            _logger = logger;
        }
        /**
         * GetUsers
         *
         * This method retrieves all User entities from the database.
         */
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        /**
         * GetUser
         *
         * This method retrieves a User entity by its ID.
         */
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return user;
        }

        /**
         * GetUserByStudentNumber
         *
         * This method retrieves a User entity by its student number.
         */
        [HttpGet("search/{studentNumber}")]
        public async Task<ActionResult<object>> SearchUserByNumber(string studentNumber)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (user == null)
            {
                return NotFound(new { exists = false });
            }
            return new { exists = true, user };
        }

        /**
         * IsUserAdmin
         *
         * This method checks if a user is an admin.
         */
        [HttpGet("IsUserAdmin/{username}")]
        public IActionResult IsUserAdmin(string username)
        {
            var isAdmin = _context.Users.Any(u => u.StudentNumber == username && u.IsAdmin);
            return Ok(new { IsAdmin = isAdmin });
        }

        /**
         * PostUser
         *
         * This method creates a new User entity in the database.
         */
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var today = DateTime.Today;
            var futureSessions = await _context.Sessions
                .Where(s => s.Year == user.Year && s.Date >= today)
                .ToListAsync();
            foreach (var session in futureSessions)
            {
                var alreadyExists = await _context.Attendances.AnyAsync(a => a.SessionId == session.Id && a.StudentId == user.Id);
                if (!alreadyExists)
                {
                    var attendance = new Attendance
                    {
                        SessionId = session.Id,
                        StudentId = user.Id,
                        Status = AttendanceStatus.Absent
                    };
                    _context.Attendances.Add(attendance);
                }
            }
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        /**
         * PutUser
         *
         * This method updates an existing User entity in the database.
         */
        [HttpPut("{studentNumber}")]
        public async Task<IActionResult> PutUser(string studentNumber, User user)
        {
            _logger.LogInformation($"Received PUT request for user with student number: {studentNumber}");
            _logger.LogInformation($"User data: {System.Text.Json.JsonSerializer.Serialize(user)}");
            if (studentNumber != user.StudentNumber)
            {
                return BadRequest();
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (existingUser == null)
            {
                return NotFound();
            }

            existingUser.Name = user.Name;
            existingUser.Firstname = user.Firstname;
            existingUser.Email = user.Email;
            existingUser.Year = user.Year;
            existingUser.IsDelegate = user.IsDelegate;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "Erreur lors de la mise à jour de l'utilisateur.");
            }
            return NoContent();
        }

        /**
         * DeleteUser
         *
         * This method deletes a User entity from the database.
         */
        [HttpDelete("{studentNumber}")]
        public async Task<IActionResult> DeleteUser(string studentNumber)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (user == null)
            {
                return NotFound();
            }

            var today = DateTime.Today;
            var futureAttendances = await _context.Attendances
                .Include(a => a.Session)
                .Where(a => a.StudentId == user.Id && a.Session.Year == user.Year && a.Session.Date >= today)
                .ToListAsync();
            _context.Attendances.RemoveRange(futureAttendances);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /**
         * GetUserByYear
         *
         * This method retrieves all User entities for a specific year.
         */
        [HttpGet("year/{year}")]
        public async Task<ActionResult<IEnumerable<User>>> GetUserByYear(string year)
        {
            var users = await _context.Users
                .Where(s => s.Year == year)
                .ToListAsync();

            if (users == null || users.Count == 0)
            {
                return NotFound(new { message = $"Aucun étudiant trouvé pour l'année {year}" });
            }

            return users;
        }

        /**
        * SendRegisterLink
        *
        * This method sends a registration link to the user's email address.
        */
        [HttpPost("send-register-link")]
        public async Task<IActionResult> SendRegisterLink([FromBody] RegisterLinkRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == request.StudentNumber);
            if (user == null)
                return NotFound(new { message = "Étudiant introuvable." });
            if (string.IsNullOrEmpty(user.Email))
                return BadRequest(new { message = "Aucune adresse mail renseignée pour cet étudiant." });
            if (!string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest(new { message = "Un mot de passe existe déjà pour cet utilisateur. Veuillez utiliser la page de connexion." });
            if (user.RegisterTokenExpiration < DateTime.UtcNow)
            {
                user.RegisterToken = null;
                user.RegisterTokenExpiration = null;
                user.RegisterMailSent = false;
                await _context.SaveChangesAsync();
            }
            if (user.RegisterMailSent)
                return BadRequest(new { message = "Un mail a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande." });
            user.RegisterToken = Guid.NewGuid().ToString();
            user.RegisterTokenExpiration = DateTime.UtcNow.AddDays(1);
            user.RegisterMailSent = true;
            await _context.SaveChangesAsync();
            var link = $"{Environment.GetEnvironmentVariable("FRONTEND_URL")}/set-password?token={user.RegisterToken}";
            var body = $"<html><body>Bonjour {user.Firstname},<br><br>" +
                       $"Pour créer votre mot de passe, cliquez sur ce lien : <a href='{link}'>Créer mon mot de passe</a>.<br>" +
                       $"Ce lien expirera dans 24 heures.<br><br>" +
                       $"Si vous n'avez pas demandé ce mail, veuillez ignorer ce message.<br><br>" +
                       $"Cordialement,<br>L'équipe PolytechPresence</body></html>";

            try
            {
                var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtpbv.univ-lyon1.fr";
                var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
                if (!int.TryParse(smtpPortStr, out var smtpPort)) smtpPort = 587;
                var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new System.Net.NetworkCredential(
                        Environment.GetEnvironmentVariable("SMTP_USERNAME"),
                        Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    )
                };
                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? throw new InvalidOperationException("SMTP_FROM_EMAIL environment variable is not set")),
                    Subject = "Création de votre mot de passe PolytechPresence",
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(user.Email);

                mailMessage.Headers.Add("X-Priority", "1");

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de l'envoi du mail : {ex.Message}");
            }
            return Ok(new { message = "Mail envoyé." });
        }

        /**
         * SetPassword
         *
         * This method sets the password for the user.
         */
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RegisterToken == request.Token && u.RegisterTokenExpiration > DateTime.UtcNow);
            if (user == null)
                return BadRequest(new { message = "Lien invalide ou expiré." });
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return BadRequest(new { message = "Le mot de passe doit contenir au moins 6 caractères." });
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.RegisterToken = null;
            user.RegisterTokenExpiration = null;
            user.RegisterMailSent = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Mot de passe défini avec succès." });
        }
        /**
         * SetPasswordRequest
         *
         * This class represents the request body for setting a password.
         */
        public class SetPasswordRequest { public string Token { get; set; } public string Password { get; set; } }

        /**
         * RegisterLinkRequest
         *
         * This class represents the request body for sending a registration link.
         */
        public class RegisterLinkRequest
        {
            public string StudentNumber { get; set; }
        }

        /**
         * Login
         *
         * This method handles user login.
         */
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == request.StudentNumber);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return Unauthorized(new { message = "Identifiant ou mot de passe incorrect." });
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { message = "Identifiant ou mot de passe incorrect." });
            return Ok(new
            {
                studentId = user.StudentNumber,
                firstname = user.Firstname,
                lastname = user.Name,
                email = user.Email,
                isAdmin = user.IsAdmin,
                isDelegate = user.IsDelegate,
                existsInDb = true
            });
        }
        /**
         * LoginRequest
         *
         * This class represents the request body for user login.
         */
        public class LoginRequest { public string StudentNumber { get; set; } public string Password { get; set; } }

        /**
         * ForgotPassword
         *
         * This method handles the forgot password functionality.
         */
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] RegisterLinkRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == "p2203381");
            if (user == null || string.IsNullOrEmpty(user.Email))
                return Ok(new { message = "Pas de data suffisante." });
            if (user.RegisterTokenExpiration < DateTime.UtcNow)
            {
                user.RegisterToken = null;
                user.RegisterTokenExpiration = null;
                user.RegisterMailSent = false;
                await _context.SaveChangesAsync();
            }

            if (user.RegisterMailSent)
                return Ok(new { message = "Un mail de réinitialisation a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande." });
            user.RegisterToken = Guid.NewGuid().ToString();
            user.RegisterTokenExpiration = DateTime.UtcNow.AddHours(1);
            user.RegisterMailSent = true;
            await _context.SaveChangesAsync();
            var link = $"{Environment.GetEnvironmentVariable("FRONTEND_URL")}/set-password?token={user.RegisterToken}";
            var body = $"Bonjour {user.Firstname},<br>Pour réinitialiser votre mot de passe, cliquez sur ce lien : <a href='{link}'>Réinitialiser mon mot de passe</a><br>Ce lien expirera dans 1h.";

            try
            {
                var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtpbv.univ-lyon1.fr";
                var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
                if (!int.TryParse(smtpPortStr, out var smtpPort)) smtpPort = 587;
                var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new System.Net.NetworkCredential(
                        Environment.GetEnvironmentVariable("SMTP_USERNAME"),
                        Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    )
                };
                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? throw new InvalidOperationException("SMTP_FROM_EMAIL environment variable is not set")),
                    Subject = "Réinitialisation de votre mot de passe PolytechPresence",
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(user.Email);
                mailMessage.Headers.Add("X-Priority", "1");
                _logger.LogInformation($"Sending password reset email to {user.Email} with link: {link}");

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch
            {
                return StatusCode(500, "Erreur lors de l'envoi du mail.");
            }

            return Ok(new { message = "Si un compte existe, un mail de réinitialisation a été envoyé." });
        }
        /**
         * UserExists
         *
         * This method checks if a user exists by ID.
         */
        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        /**
         * UserExistsByStudentNumber
         *
         * This method checks if a user exists by student number.
         */
        [HttpGet("have-password/{studentNumber}")]
        public async Task<IActionResult> HavePassword(string studentNumber)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (user == null)
                return NotFound(new { message = "Étudiant introuvable." });
            return Ok(new { havePassword = !string.IsNullOrEmpty(user.PasswordHash) });
        }

        /**
         * MakeAdmin
         *
         * This method promotes a user to admin.
         */
        [HttpPost("make-admin/{studentNumber}")]
        public async Task<IActionResult> MakeAdmin(string studentNumber)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (user == null)
                return NotFound(new { message = "Étudiant introuvable." });
            user.IsAdmin = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "L'étudiant a été promu administrateur." });
        }

    }
}