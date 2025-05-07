using System.Text.Json.Serialization;
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
        public Session Session { get; set; } = null!;
        public int StudentId { get; set; }
        public User User { get; set; } = null!; 
        public AttendanceStatus Status { get; set; }
    }
}