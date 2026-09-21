using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace backend.Models
{
    /// <summary>
    /// Groupe visé par une séance. Relation n↔n indispensable : une même séance peut viser
    /// 1, 5, 8 ou 16 sous-groupes (les TP de MECA 3A découpent la promo en 5/6/5, à cheval
    /// sur la frontière série 1 / série 2).
    /// </summary>
    public class SessionGroup
    {
        public int SessionId { get; set; }

        [JsonIgnore]
        [ValidateNever]
        public Session Session { get; set; } = null!;

        public int GroupId { get; set; }

        [JsonIgnore]
        [ValidateNever]
        public Group Group { get; set; } = null!;
    }
}
