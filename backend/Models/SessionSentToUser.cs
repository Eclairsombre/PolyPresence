namespace backend.Models
{
    public class SessionSentToUser
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public DateTime SentAt { get; set; }
    }
}