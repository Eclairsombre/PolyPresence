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
        public int StudentId { get; set; }
        public AttendanceStatus Status { get; set; }
        public Session Session { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}