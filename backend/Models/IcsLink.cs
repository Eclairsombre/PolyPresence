using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace backend.Models
{
    public class IcsLink
    {
        [Key]
        public int Id { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Nature des groupes découverts par ce lien : sous-groupes de promo, LV1 ou LV2.
        /// </summary>
        public GroupType Kind { get; set; } = GroupType.Sub;

        /// <summary>
        /// Libellé ADE désignant la promotion entière, ex. "Diplôme d'Ingénieur POLYTECH 3A
        /// (Informatique)". C'est LA seule correspondance qu'on ne peut pas déduire des données :
        /// rien dans l'ICS ne relie ce libellé à "INFO 1-A".."INFO 2-H". Une séance qui le vise
        /// est étendue à tous les sous-groupes enfants. Laisser vide pour un lien LV.
        /// Indice pour le pré-remplir : c'est le libellé le plus fréquent du calendrier
        /// (INFO 3A : 185 séances sur 298 ; MECA 3A : 165 sur 420).
        /// </summary>
        public string? PromoLabel { get; set; }

        public int SpecializationId { get; set; }
        [JsonIgnore]
        [ValidateNever]
        public Specialization Specialization { get; set; } = null!;
    }
}