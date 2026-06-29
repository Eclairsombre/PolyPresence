namespace backend.Models
{
    public enum OutboxEmailStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2,
    }

    /// <summary>
    /// File d'attente d'emails (pattern outbox). Les contrôleurs y déposent un email
    /// (rapide, transactionnel avec la requête), et un worker en arrière-plan les envoie
    /// par lots avec retry/backoff. Évite de bloquer les requêtes/le cron sur le SMTP.
    /// </summary>
    public class OutboxEmail
    {
        public int Id { get; set; }
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;

        public OutboxEmailStatus Status { get; set; } = OutboxEmailStatus.Pending;
        public int Attempts { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Prochaine tentative possible (UTC) — utilisé pour le backoff.</summary>
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
        public string? LastError { get; set; }
    }
}
