using Microsoft.AspNetCore.Routing.Constraints;
using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    /// <summary>Valeurs possibles pour <see cref="User.NotificationMode"/>.</summary>
    public static class UserNotificationMode
    {
        public const string Email = "Email";
        public const string Account = "Account";
    }

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

        /// <summary>
        /// Indique que cet utilisateur est un professeur (issu de la fusion de
        /// l'ancienne entité Professor). Les sessions le référencent via ProfId/ProfId2.
        /// </summary>
        public bool IsProfessor { get; set; } = false;

        /// <summary>
        /// Préférence de notification du professeur lorsqu'une feuille est prête à
        /// signer : "Email" (défaut, reçoit un mail) ou "Account" (consulte son espace).
        /// </summary>
        public string NotificationMode { get; set; } = "Email";

        public string? PasswordHash { get; set; }
        public string? RegisterToken { get; set; }
        public DateTime? RegisterTokenExpiration { get; set; }
        public bool RegisterMailSent { get; set; } = false;

        public int? MailPreferencesId { get; set; }
        public MailPreferences? MailPreferences { get; set; }
        public List<Attendance> Attendances { get; set; } = new List<Attendance>();

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public int? SpecializationId { get; set; }
        public Specialization? Specialization { get; set; }
    }
}
