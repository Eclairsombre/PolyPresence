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

        public int SpecializationId { get; set; }
        [JsonIgnore]
        [ValidateNever]
        public Specialization Specialization { get; set; } = null!;
    }
}