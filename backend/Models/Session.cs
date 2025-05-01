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

        public string ValidationCode { get; set; } = string.Empty;
        [JsonIgnore]
        public List<Attendance> Attendances { get; set; } = new List<Attendance>();
        public bool IsSent { get; set; } = false;
    }
}