using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class IcsLink
    {
        [Key]
        public int Id { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}