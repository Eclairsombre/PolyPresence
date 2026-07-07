using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<Attendance> Attendances { get; set; } = null!;
        public DbSet<MailPreferences> MailPreferences { get; set; }

        public DbSet<SessionSentToUser> SessionSentToUsers { get; set; }
        public DbSet<IcsLink> IcsLinks { get; set; }
        public DbSet<Specialization> Specializations { get; set; } = null!;
        public DbSet<OutboxEmail> OutboxEmails { get; set; } = null!;


        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
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

            modelBuilder.Entity<Specialization>()
                .HasIndex(s => s.Code)
                .IsUnique();

            modelBuilder.Entity<Session>()
                .HasOne(s => s.Specialization)
                .WithMany()
                .HasForeignKey(s => s.SpecializationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index sur Sessions : c'est la table la plus volumineuse et la plus requêtée.
            // Sans eux, chaque requête (planning du jour, "cours en cours" du SSE toutes les
            // quelques secondes, recherche par token de signature prof) fait un scan séquentiel.
            modelBuilder.Entity<Session>().HasIndex(s => s.Date);                 // "cours en cours" (Date == today)
            modelBuilder.Entity<Session>().HasIndex(s => new { s.Year, s.Date }); // liste paginée (filtre Year + tri Date)
            modelBuilder.Entity<Session>().HasIndex(s => s.ProfSignatureToken);   // lookup lien de signature prof
            modelBuilder.Entity<Session>().HasIndex(s => s.ProfSignatureToken2);
            modelBuilder.Entity<Session>().HasIndex(s => s.ProfId);               // sessions d'un prof
            modelBuilder.Entity<Session>().HasIndex(s => s.ProfId2);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Specialization)
                .WithMany()
                .HasForeignKey(u => u.SpecializationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IcsLink>()
                .HasOne(l => l.Specialization)
                .WithMany()
                .HasForeignKey(l => l.SpecializationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IcsLink>()
                .HasIndex(l => new { l.SpecializationId, l.Year })
                .IsUnique();

            // Index pour la requête de poll du worker d'envoi d'emails.
            modelBuilder.Entity<OutboxEmail>()
                .HasIndex(e => new { e.Status, e.NextAttemptAt });

            // Index de connexion : le login recherche par StudentNumber OU Email.
            // Sans index, c'est un scan séquentiel de Users à chaque connexion.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.StudentNumber);

            // Unicité partielle de l'email (le login par email suppose l'unicité) :
            // on exclut uniquement les emails vides (profs importés sans email).
            // Le filtre se limite à `Email <> ''` pour que la requête de login
            // (qui contient `Email <> '' AND Email = @id`) puisse utiliser cet index.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("\"Email\" <> ''");

        }
    }
}