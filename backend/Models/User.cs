using Microsoft.AspNetCore.Routing.Constraints;
using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
        public bool IsDelegate { get; set; } = false;

        public string? PasswordHash { get; set; }
        public string? RegisterToken { get; set; }
        public DateTime? RegisterTokenExpiration { get; set; }
        public bool RegisterMailSent { get; set; } = false;

        public int? MailPreferencesId { get; set; }
        public MailPreferences? MailPreferences { get; set; }
        public List<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
