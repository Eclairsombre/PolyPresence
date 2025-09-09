using System.Text.Json.Serialization;

namespace backend.Models
{
    public class Session
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string ValidationCode { get; set; } = string.Empty;
        public string ProfName { get; set; } = string.Empty;
        public string ProfFirstname { get; set; } = string.Empty;
        public string ProfEmail { get; set; } = string.Empty;
        public string? ProfSignature { get; set; }
        public string? ProfSignatureToken { get; set; }

        public string? ProfName2 { get; set; }
        public string? ProfFirstname2 { get; set; }
        public string? ProfEmail2 { get; set; }
        public string? ProfSignature2 { get; set; }
        public string? ProfSignatureToken2 { get; set; }

        public string TargetGroup { get; set; } = string.Empty;
        [JsonIgnore]
        public List<Attendance> Attendances { get; set; } = new List<Attendance>();
        public bool IsSent { get; set; } = false;
        public bool IsMailSent { get; set; } = false;
        public bool IsMailSent2 { get; set; } = false; 
        public bool IsMerged { get; set; } = false;
    }
}