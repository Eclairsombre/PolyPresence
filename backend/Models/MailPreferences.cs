namespace backend.Models
{
    public class MailPreferences
    {
        public int Id { get; set; }
        public string EmailTo { get; set; } = string.Empty;
        public List<string> Days { get; set; } = new List<string>();
        public bool Active { get; set; } = false;
    }

}