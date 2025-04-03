namespace backend.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Firstname { get; set; } = string.Empty;

        public string StudentNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}