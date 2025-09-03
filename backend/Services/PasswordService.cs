using System.Text.RegularExpressions;

namespace backend.Services
{
    public interface IPasswordService
    {
        bool ValidatePassword(string password);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
        (bool IsValid, List<string> Errors) ValidatePasswordStrength(string password);
    }

    public class PasswordService : IPasswordService
    {
        private readonly ILogger<PasswordService> _logger;

        public PasswordService(ILogger<PasswordService> logger)
        {
            _logger = logger;
        }

        public bool ValidatePassword(string password)
        {
            var (isValid, _) = ValidatePasswordStrength(password);
            return isValid;
        }

        public (bool IsValid, List<string> Errors) ValidatePasswordStrength(string password)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(password))
            {
                errors.Add("Le mot de passe est requis.");
                return (false, errors);
            }

            if (password.Length < 8)
            {
                errors.Add("Le mot de passe doit contenir au moins 8 caractères.");
            }

            if (!Regex.IsMatch(password, @"[A-Z]"))
            {
                errors.Add("Le mot de passe doit contenir au moins une lettre majuscule.");
            }

            if (!Regex.IsMatch(password, @"[a-z]"))
            {
                errors.Add("Le mot de passe doit contenir au moins une lettre minuscule.");
            }

            if (!Regex.IsMatch(password, @"\d"))
            {
                errors.Add("Le mot de passe doit contenir au moins un chiffre.");
            }

            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>?]"))
            {
                errors.Add("Le mot de passe doit contenir au moins un caractère spécial.");
            }

            return (errors.Count == 0, errors);
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public bool VerifyPassword(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password hash");
                return false;
            }
        }
    }
}
