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


        // GET: api/User
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/User/5
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

        // GET: api/User/search/{studentNumber}
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

        // GET: api/User/IsUserAdmin/{username}
        [HttpGet("IsUserAdmin/{username}")]
        public IActionResult IsUserAdmin(string username)
        {
            var isAdmin = _context.Users.Any(u => u.StudentNumber == username && u.IsAdmin);
            return Ok(new { IsAdmin = isAdmin });
        }

        // POST: api/User
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

        // PUT: api/User/5
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

        // DELETE: api/User/5
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


        [HttpGet("year/{year}")]
        public async Task<ActionResult<IEnumerable<User>>> GetUserByYear(string year)
        {
            var users = await _context.Users
                .Where(s => s.IsAdmin == false)
                .Where(s => s.Year == year)
                .ToListAsync();

            if (users == null || users.Count == 0)
            {
                return NotFound(new { message = $"Aucun étudiant trouvé pour l'année {year}" });
            }

            return users;
        }

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
            if (user.RegisterMailSent)
                return BadRequest(new { message = "Un mail a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande." });
            user.RegisterToken = Guid.NewGuid().ToString();
            user.RegisterTokenExpiration = DateTime.UtcNow.AddDays(1);
            user.RegisterMailSent = true;
            await _context.SaveChangesAsync();
            var link = $"{Environment.GetEnvironmentVariable("FRONTEND_URL")}/set-password?token={user.RegisterToken}";
            var body = $"Bonjour {user.Firstname},<br>Pour créer votre mot de passe, cliquez sur ce lien : <a href='{link}'>Créer mon mot de passe</a><br>Ce lien expirera dans 24h.";

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
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de l'envoi du mail : {ex.Message}");
            }
            return Ok(new { message = "Mail envoyé." });
        }

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
        public class SetPasswordRequest { public string Token { get; set; } public string Password { get; set; } }

        public class RegisterLinkRequest
        {
            public string StudentNumber { get; set; }
        }

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
        public class LoginRequest { public string StudentNumber { get; set; } public string Password { get; set; } }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] RegisterLinkRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == request.StudentNumber);
            // Toujours répondre OK pour la sécurité, même si l'utilisateur n'existe pas
            if (user == null || string.IsNullOrEmpty(user.Email))
                return Ok(new { message = "Si un compte existe, un mail de réinitialisation a été envoyé." });
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
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch { /* Ne rien faire pour la sécurité */ }

            return Ok(new { message = "Si un compte existe, un mail de réinitialisation a été envoyé." });
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}