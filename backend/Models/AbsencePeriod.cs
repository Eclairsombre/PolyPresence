using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Models
{
    /// <summary>
    /// Absence justifiée d'avance par la scolarité. Les absences de l'étudiant qu'elle couvre
    /// passent en ABJ avec son motif et son commentaire, y compris celles relevées plus tard.
    ///
    /// - Absence anticipée (hospitalisation prévue, convocation…) : une durée, du <see cref="From"/>
    ///   au <see cref="To"/> inclus, toujours sur tous les cours.
    /// - Dispense : un cours, sans dates (toutes ses séances) ; le commentaire posé est « Dispense ».
    /// </summary>
    public class AbsencePeriod
    {
        public int Id { get; set; }

        public int StudentId { get; set; }

        [JsonIgnore]
        public User Student { get; set; } = null!;

        /// <summary>Premier jour couvert (date seule) ; null pour une dispense.</summary>
        public DateTime? From { get; set; }

        /// <summary>Dernier jour couvert, inclus (date seule) ; null pour une dispense.</summary>
        public DateTime? To { get; set; }

        /// <summary>Cours de la dispense ; null pour une absence anticipée (tous les cours).</summary>
        [MaxLength(200)]
        public string? CourseName { get; set; }

        public bool IsDispense { get; set; }

        public int? ReasonId { get; set; }

        [JsonIgnore]
        public AbsenceReason? Reason { get; set; }

        /// <summary>Précision libre ; pour une dispense, s'ajoute après « Dispense ».</summary>
        [MaxLength(500)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? CreatedById { get; set; }
    }
}
