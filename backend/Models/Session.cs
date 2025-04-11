namespace backend.Models
{
    public class Session
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Year { get; set; } = string.Empty;
        public List<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}