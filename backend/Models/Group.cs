using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace backend.Models
{
    /// <summary>Nature d'un groupe, telle que déduite du lien ICS qui l'a fait découvrir.</summary>
    public enum GroupType
    {
        /// <summary>Sous-groupe d'une promotion ("INFO 1-A", "MECA5 1-A", "GBM5A groupe 07 [Ing]"...).</summary>
        Sub = 0,

        /// <summary>
        /// Libellé qui désigne la promotion entière ("Diplôme d'Ingénieur POLYTECH 3A (Informatique)").
        /// Une séance qui le vise concerne tous les sous-groupes enfants.
        /// </summary>
        Promo = 1,

        /// <summary>
        /// Groupe de langue vivante 1 (code ADE opaque, ex. "A-PL9004TR-BE91").
        /// N'est plus posé à l'import — voir <see cref="Language"/> — mais reste
        /// utilisable quand l'admin veut expliciter le créneau d'un groupe.
        /// </summary>
        Lv1 = 2,

        /// <summary>Groupe de langue vivante 2. Même remarque que <see cref="Lv1"/>.</summary>
        Lv2 = 3,

        /// <summary>
        /// Groupe de langue vivante, sans préjuger du créneau. C'est le type posé à l'import
        /// d'un calendrier de langues : un seul export ADE porte la LV1 ET la LV2, et rien
        /// dans l'ICS ne dit laquelle des deux un code désigne ("A-I3002TR-AR51" est de
        /// l'anglais, "A-I3004TR-ES51" de l'espagnol — la différence n'est lisible nulle part).
        ///
        /// Le créneau est donc porté par l'étudiant, via <c>User.Lv1GroupId</c> et
        /// <c>User.Lv2GroupId</c> : c'est le fichier d'import des étudiants qui tranche,
        /// et un même groupe peut légitimement être la LV1 de l'un et la LV2 de l'autre.
        /// </summary>
        Language = 4,
    }

    /// <summary>
    /// Groupe d'étudiants tel que nommé par ADE. Le <see cref="Label"/> est repris VERBATIM
    /// depuis la description de l'ICS : les conventions de nommage varient d'une promo à
    /// l'autre ("INFO 1-A", "INFO5 A", "MAT5 A", "MECA5 1-A", "GBM5A groupe 07 [Ing]"), donc
    /// on ne cherche jamais à les interpréter — on les compare.
    ///
    /// Les groupes sont créés automatiquement à l'import dès qu'un libellé inconnu apparaît :
    /// un libellé appartenant à une autre filière est inoffensif (aucun étudiant ne lui est
    /// rattaché, donc aucun émargement n'est généré).
    /// </summary>
    public class Group
    {
        public int Id { get; set; }

        /// <summary>Libellé ADE exact, tel qu'il apparaît dans la description de l'événement.</summary>
        [MaxLength(200)]
        public string Label { get; set; } = string.Empty;

        /// <summary>Nom lisible, modifiable par l'admin. Vaut <see cref="Label"/> par défaut.</summary>
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        public GroupType Type { get; set; } = GroupType.Sub;

        /// <summary>Groupe promo parent, pour un sous-groupe. Null pour les LV et les promos.</summary>
        public int? ParentGroupId { get; set; }

        [JsonIgnore]
        [ValidateNever]
        public Group? ParentGroup { get; set; }

        /// <summary>Filière du lien ICS qui a fait découvrir ce groupe (LANGUES pour les LV).</summary>
        public int? SpecializationId { get; set; }

        [JsonIgnore]
        [ValidateNever]
        public Specialization? Specialization { get; set; }

        /// <summary>Année du lien ICS qui a fait découvrir ce groupe ("3A", "4A"...).</summary>
        [MaxLength(20)]
        public string? Year { get; set; }

        /// <summary>Nombre de séances vues avec ce libellé au dernier import : aide l'admin à trier.</summary>
        public int SeenCount { get; set; }

        public DateTime? LastSeenAt { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
