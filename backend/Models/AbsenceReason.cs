using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    /// <summary>
    /// Motif d'absence prédéfini, géré par la scolarité (« Certificat médical », « Grève TCL »…).
    ///
    /// Un motif ne justifie jamais une absence à lui seul : la justification est une décision
    /// séparée de la scolarité (<see cref="Attendance.IsJustified"/>). On ne supprime pas un
    /// motif, on le désactive : il disparaît des listes de choix mais reste affiché sur les
    /// absences qui l'utilisent déjà.
    /// </summary>
    public class AbsenceReason
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Label { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        /// <summary>Ordre d'affichage dans les listes déroulantes (croissant).</summary>
        public int SortOrder { get; set; }
    }
}
