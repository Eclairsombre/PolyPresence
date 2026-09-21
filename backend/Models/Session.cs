using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace backend.Models
{
    public class Session
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string ValidationCode { get; set; } = string.Empty;

        /// <summary>
        /// UID de l'événement ADE ("ADE60" + hex de "Projet2026-27-&lt;idEvt&gt;-&lt;n&gt;-&lt;occ&gt;").
        /// Stable et unique par occurrence : c'est la clé de rapprochement des imports.
        /// Le rapprochement historique sur (Date, StartTime, EndTime) supprimait puis recréait
        /// la séance dès qu'un créneau bougeait, effaçant au passage les présences saisies.
        /// Null pour les séances créées à la main.
        /// </summary>
        public string? IcsUid { get; set; }

        /// <summary>
        /// Calendrier d'origine de la séance : EDT de promo, LV1 ou LV2. Sans cette
        /// distinction, l'import LV2 considérerait les séances LV1 comme disparues et les
        /// supprimerait — les deux liens partagent la même filière (LANGUES) et la même
        /// année. Le périmètre de synchronisation est donc (année, filière, calendrier),
        /// exactement la clé unique d'<see cref="IcsLink"/>.
        /// </summary>
        public GroupType IcsKind { get; set; } = GroupType.Sub;

        public int SpecializationId { get; set; }
        [JsonIgnore]
        [ValidateNever]
        public Specialization Specialization { get; set; } = null!;

        public string? ProfId { get; set; }

        public string? ProfSignature { get; set; }
        public string? ProfSignatureToken { get; set; }
        public string? ProfId2 { get; set; }
        public string? ProfSignature2 { get; set; }
        public string? ProfSignatureToken2 { get; set; }

        [JsonIgnore]
        public List<Attendance> Attendances { get; set; } = new List<Attendance>();

        /// <summary>Groupes visés par la séance. Vide = toute la promo (année + filière).</summary>
        [JsonIgnore]
        public List<SessionGroup> SessionGroups { get; set; } = new List<SessionGroup>();
        public bool IsSent { get; set; } = false;
        public bool IsMailSent { get; set; } = false;
        public bool IsMailSent2 { get; set; } = false;
        public bool IsMerged { get; set; } = false;
    }
}