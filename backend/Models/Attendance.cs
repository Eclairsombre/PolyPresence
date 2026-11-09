using System.Text.Json.Serialization;
namespace backend.Models
{
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late
    }

    public class Attendance
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public Session Session { get; set; } = null!;
        public int StudentId { get; set; }
        public User User { get; set; } = null!; 
        public AttendanceStatus Status { get; set; }

        /// <summary>
        /// Texte libre. Quand <see cref="ReasonId"/> est vide, c'est le motif « Autre »
        /// (ou un commentaire saisi avant l'introduction des motifs prédéfinis).
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>Motif prédéfini choisi pour cette absence. Null = aucun motif, ou « Autre ».</summary>
        public int? ReasonId { get; set; }

        [JsonIgnore]
        public AbsenceReason? Reason { get; set; }

        /// <summary>
        /// Justification décidée par la scolarité. Injustifiée par défaut ; choisir un motif
        /// ne change jamais cette valeur.
        /// </summary>
        public bool IsJustified { get; set; } = false;

        /// <summary>
        /// Période d'absence anticipée (ou dispense) qui a justifié cette absence ; null si la
        /// justification a été posée à la main. Sert à défaire l'effet si la période change.
        /// </summary>
        public int? AbsencePeriodId { get; set; }

        [JsonIgnore]
        public AbsencePeriod? AbsencePeriod { get; set; }
    }
}