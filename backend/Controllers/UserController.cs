using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
        private readonly ILogger<UserController> _logger;
        private readonly IJwtService _jwtService;
        private readonly IPasswordService _passwordService;
        private readonly IRateLimitService _rateLimitService;
        private readonly ICookieEncryptionService _cookieEncryptionService;

        public UserController(
            ApplicationDbContext context,
            ILogger<UserController> logger,
            IJwtService jwtService,
            IPasswordService passwordService,
            IRateLimitService rateLimitService,
            ICookieEncryptionService cookieEncryptionService)
        {
            _context = context;
            _logger = logger;
            _jwtService = jwtService;
            _passwordService = passwordService;
            _rateLimitService = rateLimitService;
            _cookieEncryptionService = cookieEncryptionService;
        }
        /**
         * GetUsers
         *
         * This method retrieves all User entities from the database.
         * Only administrators can access this endpoint.
         */
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
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

            // Vérifie si l'utilisateur est administrateur
            if (!currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé à la liste des utilisateurs par {currentUser.StudentNumber}");
                return Forbid();
            }

            return await _context.Users.ToListAsync();
        }

        /**
         * GetUser
         *
         * This method retrieves a User entity by its ID.
         * Users can only access their own data unless they are administrators.
         */
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            var requestedUser = await _context.Users.FindAsync(id);
            if (requestedUser == null)
            {
                return NotFound();
            }

            if (currentUserId != id && !currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé: {currentUser.StudentNumber} a tenté d'accéder aux données de l'utilisateur ID {id}");
                return Forbid();
            }

            return requestedUser;
        }

        /**
         * GetCurrentUser
         *
         * Returns the currently authenticated user's basic info.
         */
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            var user = await _context.Users.FindAsync(currentUserId);
            if (user == null)
            {
                return NotFound(new { message = "Utilisateur introuvable." });
            }

            var dto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Firstname = user.Firstname,
                StudentNumber = user.StudentNumber,
                Email = user.Email,
                Year = user.Year,
                IsAdmin = user.IsAdmin,
                IsDelegate = user.IsDelegate
            };

            return Ok(dto);
        }

        /**
         * GetUserByStudentNumber
         *
         * This method retrieves a User entity by its student number.
         * Users can only access their own data unless they are administrators.
         */
        [HttpGet("search/{studentNumber}")]
        [Authorize]
        public async Task<ActionResult<object>> SearchUserByNumber(string studentNumber)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            var requestedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);

            if (requestedUser == null)
            {
                return NotFound(new { exists = false });
            }

            if (currentUser.StudentNumber != requestedUser.StudentNumber && !currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé: {currentUser.StudentNumber} a tenté d'accéder aux données de {studentNumber}");
                return Forbid();
            }

            return new { exists = true, user = requestedUser };
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
         * PutUser
         *
         * This method updates an existing User entity in the database.
         * Only administrators or the user themselves can update their profile.
         */
        [HttpPut("{studentNumber}")]
        [Authorize]
        public async Task<IActionResult> PutUser(string studentNumber, User user)
        {
            _logger.LogInformation($"Received PUT request for user with student number: {studentNumber}");

            if (studentNumber != user.StudentNumber)
            {
                return BadRequest(new { message = "Le numéro étudiant ne correspond pas." });
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

            bool isAdmin = currentUser.IsAdmin;
            bool isOwnProfile = currentUser.StudentNumber == studentNumber;

            if (!isAdmin && !isOwnProfile)
            {
                _logger.LogWarning($"Tentative non autorisée de modification du profil {studentNumber} par {currentUser.StudentNumber}");
                return Forbid();
            }

            if (Request.Headers.ContainsKey("Admin-Token"))
            {
                var (adminUser, errorResult) = await GetAdminUserFromToken();
                if (errorResult != null)
                {
                    return errorResult;
                }

                _logger.LogInformation($"Admin user {adminUser!.StudentNumber} authorized the update");
            }
            else if (!isAdmin && (user.IsAdmin != currentUser.IsAdmin || user.IsDelegate != currentUser.IsDelegate))
            {
                _logger.LogWarning($"Tentative non autorisée de modification des droits pour {studentNumber} par {currentUser.StudentNumber}");
                return Forbid();
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
         * Only administrators can delete users.
         */
        [HttpDelete("{studentNumber}")]
        [Authorize]
        public async Task<IActionResult> DeleteUser(string studentNumber)
        {
            // Vérification du rôle administrateur via JWT
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            // Récupération de l'utilisateur authentifié
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            // Vérification si l'utilisateur est administrateur
            if (!currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé à la suppression d'utilisateur par {currentUser.StudentNumber}");
                return Forbid();
            }

            // Vérification supplémentaire via token d'administration
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null)
            {
                return errorResult;
            }

            _logger.LogInformation($"Admin user {adminUser!.StudentNumber} authorized the deletion");

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
         * Public endpoint to get users by year.
         */
        [HttpGet("year/{year}")]
        public async Task<ActionResult<IEnumerable<User>>> GetUserByYear(string year)
        {
            _logger.LogInformation($"Public request for users in year {year}");

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
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? throw new InvalidOperationException("SMTP_FROM_EMAIL environment variable is not set")),
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
        public class SetPasswordRequest
        {
            public required string Token { get; set; }
            public required string Password { get; set; }
        }

        /**
         * RegisterLinkRequest
         *
         * This class represents the request body for sending a registration link.
         */
        public class RegisterLinkRequest
        {
            public required string StudentNumber { get; set; }
        }

        /**
         * Login
         *
         * This method handles user login with JWT authentication.
         */
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.StudentNumber) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login attempt with missing credentials");
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "Numéro étudiant et mot de passe requis."
                });
            }

            if (!_rateLimitService.IsLoginAttemptAllowed(request.StudentNumber))
            {
                _logger.LogWarning("Rate limit exceeded for login attempt: {StudentNumber}", request.StudentNumber);
                return StatusCode(429, new LoginResponse
                {
                    Success = false,
                    Message = "Trop de tentatives de connexion. Veuillez patienter avant de réessayer."
                });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == request.StudentNumber);

                if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogWarning("Login attempt with invalid student number: {StudentNumber}", request.StudentNumber);
                    return Unauthorized(new LoginResponse
                    {
                        Success = false,
                        Message = "Identifiant ou mot de passe incorrect."
                    });
                }

                if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login attempt with invalid password for user: {StudentNumber}", request.StudentNumber);
                    return Unauthorized(new LoginResponse
                    {
                        Success = false,
                        Message = "Identifiant ou mot de passe incorrect."
                    });
                }

                _rateLimitService.ResetLoginAttempts(request.StudentNumber);

                var tokenResponse = await _jwtService.GenerateTokensAsync(user);

                _logger.LogInformation("Successful login for user: {StudentNumber}", user.StudentNumber);

                // Prepare user info to store in a HttpOnly encrypted cookie
                var userInfo = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Firstname = user.Firstname,
                    StudentNumber = user.StudentNumber,
                    Email = user.Email,
                    Year = user.Year,
                    IsAdmin = user.IsAdmin,
                    IsDelegate = user.IsDelegate
                };

                var userInfoJson = System.Text.Json.JsonSerializer.Serialize(userInfo);
                var protectedUserInfo = _cookieEncryptionService.Protect(userInfoJson);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    Expires = DateTime.UtcNow.AddDays(7)
                };

                Response.Cookies.Append("user_info", protectedUserInfo, cookieOptions);

                return Ok(new LoginResponse
                {
                    Success = true,
                    Message = "Connexion réussie",
                    Token = tokenResponse,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Firstname = user.Firstname,
                        StudentNumber = user.StudentNumber,
                        Email = user.Email,
                        Year = user.Year,
                        IsAdmin = user.IsAdmin,
                        IsDelegate = user.IsDelegate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {StudentNumber}", request.StudentNumber);
                return StatusCode(500, new LoginResponse
                {
                    Success = false,
                    Message = "Erreur interne du serveur."
                });
            }
        }

        /**
         * RefreshToken
         *
         * This method refreshes an expired JWT token using a refresh token.
         */
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            _logger.LogInformation("RefreshToken endpoint called with payload: {RequestData}",
                System.Text.Json.JsonSerializer.Serialize(request));

            if (request == null)
            {
                _logger.LogWarning("RefreshToken: Request body is null");
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "Requête invalide: corps de la requête manquant."
                });
            }

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                _logger.LogWarning("RefreshToken: RefreshToken is null or empty");
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "Refresh token requis."
                });
            }

            try
            {
                var refreshedTokens = await _jwtService.RefreshTokenAsync(request.RefreshToken);

                if (refreshedTokens == null)
                {
                    _logger.LogWarning("Invalid refresh token attempt");
                    return Unauthorized(new LoginResponse
                    {
                        Success = false,
                        Message = "Refresh token invalide ou expiré."
                    });
                }

                var userId = _jwtService.GetUserIdFromRefreshToken(refreshedTokens.RefreshToken);
                if (userId == null)
                {
                    _logger.LogWarning("User ID could not be retrieved from new refresh token");
                    return Unauthorized(new LoginResponse
                    {
                        Success = false,
                        Message = "Erreur lors du renouvellement du token."
                    });
                }

                // Récupérer les données de l'utilisateur
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("User not found for refresh token: {UserId}", userId);
                    return Unauthorized(new LoginResponse
                    {
                        Success = false,
                        Message = "Utilisateur introuvable."
                    });
                }

                // Générer uniquement un nouveau token d'accès sans créer un nouveau refresh token
                var accessToken = await _jwtService.GenerateAccessTokenOnlyAsync(user);

                // Mettre à jour les informations du token retourné
                refreshedTokens.AccessToken = accessToken;

                _logger.LogInformation("Token refreshed for user: {StudentNumber}", user.StudentNumber);

                // Update HttpOnly encrypted cookie with refreshed user info
                var userInfo = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Firstname = user.Firstname,
                    StudentNumber = user.StudentNumber,
                    Email = user.Email,
                    Year = user.Year,
                    IsAdmin = user.IsAdmin,
                    IsDelegate = user.IsDelegate
                };

                var userInfoJson = System.Text.Json.JsonSerializer.Serialize(userInfo);
                var protectedUserInfo = _cookieEncryptionService.Protect(userInfoJson);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    Expires = DateTime.UtcNow.AddDays(7)
                };

                Response.Cookies.Append("user_info", protectedUserInfo, cookieOptions);

                return Ok(new LoginResponse
                {
                    Success = true,
                    Message = "Token renouvelé avec succès",
                    Token = refreshedTokens,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Firstname = user.Firstname,
                        StudentNumber = user.StudentNumber,
                        Email = user.Email,
                        Year = user.Year,
                        IsAdmin = user.IsAdmin,
                        IsDelegate = user.IsDelegate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new LoginResponse
                {
                    Success = false,
                    Message = "Erreur interne du serveur."
                });
            }
        }

        /**
         * Logout
         *
         * This method logs out a user by revoking their JWT token.
         */
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    await _jwtService.RevokeTokenAsync(token);
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    await _jwtService.RevokeAllUserTokensAsync(userId);
                    _logger.LogInformation("User logged out successfully: {UserId}", userId);
                }

                // Clear user_info cookie on logout
                Response.Cookies.Delete("user_info", new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                });

                return Ok(new { Success = true, Message = "Déconnexion réussie" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Success = false, Message = "Erreur interne du serveur." });
            }
        }

        /**
         * ChangePassword
         *
         * This method allows a user to change their password.
         */
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { Success = false, Message = "Mot de passe actuel et nouveau mot de passe requis." });
            }

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { Success = false, Message = "Utilisateur non identifié." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                {
                    return NotFound(new { Success = false, Message = "Utilisateur introuvable." });
                }

                if (!_passwordService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid current password for user: {StudentNumber}", user.StudentNumber);
                    return Unauthorized(new { Success = false, Message = "Mot de passe actuel incorrect." });
                }

                var (isValid, errors) = _passwordService.ValidatePasswordStrength(request.NewPassword);
                if (!isValid)
                {
                    return BadRequest(new { Success = false, Message = "Nouveau mot de passe invalide.", Errors = errors });
                }

                user.PasswordHash = _passwordService.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                await _jwtService.RevokeAllUserTokensAsync(userId);

                _logger.LogInformation("Password changed successfully for user: {StudentNumber}", user.StudentNumber);

                return Ok(new { Success = true, Message = "Mot de passe modifié avec succès. Veuillez vous reconnecter." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password change");
                return StatusCode(500, new { Success = false, Message = "Erreur interne du serveur." });
            }
        }
        /**
         * LoginRequest
         *
         * This class represents the request body for user login.
         */
        public class LoginRequest
        {
            public required string StudentNumber { get; set; }
            public required string Password { get; set; }
        }

        /**
         * ForgotPassword
         *
         * This method handles the forgot password functionality.
         */
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            // Validation des paramètres d'entrée
            if (string.IsNullOrWhiteSpace(request.StudentNumber))
            {
                return BadRequest(new { Success = false, Message = "Numéro étudiant requis." });
            }

            // Rate limiting pour les demandes de reset
            if (!_rateLimitService.IsPasswordResetAllowed(request.StudentNumber))
            {
                _logger.LogWarning("Rate limit exceeded for password reset: {StudentNumber}", request.StudentNumber);
                return StatusCode(429, new { Success = false, Message = "Trop de demandes de réinitialisation. Veuillez patienter avant de réessayer." });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == request.StudentNumber);

                // Toujours retourner une réponse positive pour éviter l'énumération d'utilisateurs
                if (user == null || string.IsNullOrEmpty(user.Email))
                {
                    _logger.LogWarning("Password reset attempted for non-existent user: {StudentNumber}", request.StudentNumber);
                    return Ok(new { Success = true, Message = "Si ce numéro étudiant existe, un email de réinitialisation a été envoyé." });
                }

                // Vérifier si un token est encore valide
                if (user.RegisterTokenExpiration > DateTime.UtcNow && user.RegisterMailSent)
                {
                    return Ok(new { Success = true, Message = "Un email de réinitialisation a déjà été envoyé récemment." });
                }

                // Générer un nouveau token sécurisé
                user.RegisterToken = Guid.NewGuid().ToString();
                user.RegisterTokenExpiration = DateTime.UtcNow.AddHours(1);
                user.RegisterMailSent = true;
                await _context.SaveChangesAsync();

                var link = $"{Environment.GetEnvironmentVariable("FRONTEND_URL")}/reset-password?token={user.RegisterToken}";
                var body = $"Bonjour {user.Firstname},<br>Pour réinitialiser votre mot de passe, cliquez sur ce lien : <a href='{link}'>Réinitialiser mon mot de passe</a><br>Ce lien expirera dans 1h.";

                await SendEmailAsync(user.Email, "Réinitialisation de mot de passe - PolytechPresence", body);

                _logger.LogInformation("Password reset email sent to user: {StudentNumber}", user.StudentNumber);
                return Ok(new { Success = true, Message = "Si ce numéro étudiant existe, un email de réinitialisation a été envoyé." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for user: {StudentNumber}", request.StudentNumber);
                return StatusCode(500, new { Success = false, Message = "Erreur interne du serveur." });
            }
        }

        /**
         * ResetPassword
         *
         * This method handles password reset with token validation.
         */
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] PasswordResetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { Success = false, Message = "Token et nouveau mot de passe requis." });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.RegisterToken == request.Token);

                if (user == null || user.RegisterTokenExpiration <= DateTime.UtcNow)
                {
                    _logger.LogWarning("Invalid or expired password reset token: {Token}", request.Token.Substring(0, 8));
                    return BadRequest(new { Success = false, Message = "Token invalide ou expiré." });
                }

                // Valider le nouveau mot de passe
                var (isValid, errors) = _passwordService.ValidatePasswordStrength(request.NewPassword);
                if (!isValid)
                {
                    return BadRequest(new { Success = false, Message = "Mot de passe invalide.", Errors = errors });
                }

                // Mettre à jour le mot de passe
                user.PasswordHash = _passwordService.HashPassword(request.NewPassword);
                user.RegisterToken = null;
                user.RegisterTokenExpiration = null;
                user.RegisterMailSent = false;
                await _context.SaveChangesAsync();

                // Révoquer tous les tokens existants
                await _jwtService.RevokeAllUserTokensAsync(user.Id);

                _logger.LogInformation("Password reset successfully for user: {StudentNumber}", user.StudentNumber);
                return Ok(new { Success = true, Message = "Mot de passe réinitialisé avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                return StatusCode(500, new { Success = false, Message = "Erreur interne du serveur." });
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
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

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? throw new InvalidOperationException("SMTP_FROM_EMAIL environment variable is not set")),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);
                mailMessage.Headers.Add("X-Priority", "1");

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to: {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to: {Email}", toEmail);
                throw; // Re-throw pour que l'appelant puisse gérer l'erreur
            }
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
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null)
            {
                return errorResult;
            }

            _logger.LogInformation($"Admin user {adminUser!.StudentNumber} is promoting user {studentNumber} to admin");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);
            if (user == null)
                return NotFound(new { message = "Étudiant introuvable." });
            user.IsAdmin = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "L'étudiant a été promu administrateur." });
        }

        /**
         * GenerateAdminToken
         *
         * This method generates a secure authentication token for admin operations.
         * Uses JWT Bearer token for authentication instead of credentials in request body.
         */
        [HttpPost("generate-admin-token")]
        [Authorize]
        public async Task<IActionResult> GenerateAdminToken([FromBody] object? _ = null)
        {
            try
            {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                {
                    return BadRequest(new { message = "Token d'authentification invalide." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "Utilisateur introuvable." });
                }

                if (!user.IsAdmin)
                {
                    _logger.LogWarning($"Utilisateur non-admin {user.StudentNumber} a tenté de générer un token admin");
                    return Forbid();
                }

                var tokenService = HttpContext.RequestServices.GetRequiredService<AdminTokenService>();
                var tokenValue = tokenService.GenerateToken(user.Id);
                var tokenExpiration = DateTime.UtcNow.AddHours(24);

                _logger.LogInformation($"Admin token generated for user {user.StudentNumber}");

                return Ok(new { token = tokenValue, expiresAt = tokenExpiration });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération du token admin");
                return StatusCode(500, new { message = "Une erreur s'est produite lors de la génération du token admin." });
            }
        }        /**
         * PostUserWithToken
         *
         * This method creates a new User entity in the database using admin token authentication.
         * Only administrators can access this endpoint.
         */
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostUser([FromBody] User user)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
            {
                return Unauthorized(new { message = "Identification utilisateur incorrecte." });
            }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null)
            {
                return NotFound(new { message = "Utilisateur connecté introuvable." });
            }

            if (!currentUser.IsAdmin)
            {
                _logger.LogWarning($"Tentative d'accès non autorisé à la création d'utilisateur par {currentUser.StudentNumber}");
                return Forbid();
            }

            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null)
            {
                return errorResult;
            }

            _logger.LogInformation($"Admin user {adminUser!.StudentNumber} is creating a new user");

            if (await _context.Users.AnyAsync(u => u.StudentNumber == user.StudentNumber))
            {
                return Conflict(new { error = true, message = "Un utilisateur avec ce numéro étudiant existe déjà." });
            }

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
         * ValidateAdminToken
         *
         * Helper method to validate admin token and get the associated user
         */
        private async Task<User?> ValidateAdminToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            // Utilisation du service pour valider le token
            var tokenService = HttpContext.RequestServices.GetRequiredService<AdminTokenService>();
            var userId = tokenService.ValidateToken(token);

            if (userId == null)
                return null;

            // Récupérer l'utilisateur par son ID
            var adminUser = await _context.Users.FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.IsAdmin);

            return adminUser;
        }

        /**
         * GetAdminUserFromToken
         *
         * Helper method to get and validate admin user from request headers
         */
        private async Task<(User? AdminUser, IActionResult? ErrorResult)> GetAdminUserFromToken()
        {
            string? adminToken = Request.Headers["Admin-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(adminToken))
            {
                return (null, Unauthorized(new { message = "Token d'authentification manquant. Veuillez fournir un token admin." }));
            }

            _logger.LogInformation($"Validating admin token: {adminToken.Substring(0, 8)}...");
            var adminUser = await ValidateAdminToken(adminToken);
            if (adminUser == null)
            {
                _logger.LogWarning("Token admin invalide ou expiré");
                return (null, Unauthorized(new { message = "Token d'authentification invalide ou expiré." }));
            }

            _logger.LogInformation($"Admin token validated for user: {adminUser.StudentNumber}");
            return (adminUser, null);
        }
    }
}