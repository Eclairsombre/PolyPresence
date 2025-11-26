using Microsoft.EntityFrameworkCore;
using backend.Models;
using Microsoft.Extensions.Logging;

namespace backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ILogger<ApplicationDbContext>? _logger;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ILogger<ApplicationDbContext> logger)
            : base(options)
        {
            _logger = logger;
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<Attendance> Attendances { get; set; } = null!;
        public DbSet<MailPreferences> MailPreferences { get; set; }

        public DbSet<SessionSentToUser> SessionSentToUsers { get; set; }
        public DbSet<IcsLink> IcsLinks { get; set; }

        public override int SaveChanges()
        {
            LogSessionChanges();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            LogSessionChanges();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void LogSessionChanges()
        {
            if (_logger == null) return;

            var sessionEntries = ChangeTracker.Entries<Session>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in sessionEntries)
            {
                var session = entry.Entity;
                var stackTrace = Environment.StackTrace;

                if (entry.State == EntityState.Modified)
                {
                    var modifiedProperties = entry.Properties
                        .Where(p => p.IsModified)
                        .Select(p => $"{p.Metadata.Name}: {p.OriginalValue} → {p.CurrentValue}")
                        .ToList();

                    if (modifiedProperties.Any())
                    {
                        var logMessage = $"🔄 SESSION MODIFIÉE ID={session.Id} à {DateTime.Now:HH:mm:ss}\n" +
                                       $"  Propriétés modifiées: {string.Join(", ", modifiedProperties)}\n" +
                                       $"  Stack trace: {stackTrace.Split('\n').Take(5).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).FirstOrDefault()}";

                        _logger.LogWarning(logMessage);
                        WriteToLogFile(logMessage);

                        // PROTECTION ULTIME : Annuler les modifications des horaires d'une session fusionnée
                        var isMergedOriginal = entry.Property("IsMerged").OriginalValue;
                        if (isMergedOriginal is bool merged && merged)
                        {
                            var startTimeModified = entry.Property("StartTime").IsModified;
                            var endTimeModified = entry.Property("EndTime").IsModified;
                            var isMergedModified = entry.Property("IsMerged").IsModified;

                            if (startTimeModified || endTimeModified || isMergedModified)
                            {
                                var errorMessage = $"❌ TENTATIVE DE MODIFICATION DES HORAIRES/MERGE D'UNE SESSION FUSIONNÉE BLOQUÉE !\n" +
                                                 $"  Annulation des modifications pour session ID={session.Id}";

                                _logger.LogError(errorMessage);
                                WriteToLogFile(errorMessage);

                                // Restaurer les valeurs originales
                                if (startTimeModified)
                                    entry.Property("StartTime").CurrentValue = entry.Property("StartTime").OriginalValue;
                                if (endTimeModified)
                                    entry.Property("EndTime").CurrentValue = entry.Property("EndTime").OriginalValue;
                                if (isMergedModified)
                                    entry.Property("IsMerged").CurrentValue = entry.Property("IsMerged").OriginalValue;

                                entry.Property("StartTime").IsModified = false;
                                entry.Property("EndTime").IsModified = false;
                                entry.Property("IsMerged").IsModified = false;
                            }
                        }
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var deleteMessage = $"❌ SESSION SUPPRIMÉE ID={session.Id} à {DateTime.Now:HH:mm:ss}\n" +
                                      $"  Date={session.Date:yyyy-MM-dd}, {session.StartTime}-{session.EndTime}";

                    _logger.LogWarning(deleteMessage);
                    WriteToLogFile(deleteMessage);

                    // PROTECTION ULTIME : Empêcher la suppression d'une session fusionnée
                    if (session.IsMerged)
                    {
                        var blockMessage = $"❌ TENTATIVE DE SUPPRESSION D'UNE SESSION FUSIONNÉE BLOQUÉE !\n" +
                                         $"  Annulation de la suppression pour session ID={session.Id}";

                        _logger.LogError(blockMessage);
                        WriteToLogFile(blockMessage);
                        entry.State = EntityState.Unchanged;
                    }
                }
            }
        }

        private void WriteToLogFile(string message)
        {
            try
            {
                var storagePath = Environment.GetEnvironmentVariable("STORAGE_PATH") ?? "/data";
                var logFilePath = Path.Combine(storagePath, "session_modifications.log");

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(logFilePath, logEntry);
            }
            catch
            {
                // Ignorer les erreurs d'écriture de log
            }
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Attendances)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attendance>()
                .HasIndex(a => new { a.SessionId, a.StudentId })
                .IsUnique();


        }
    }
}