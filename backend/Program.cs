using Microsoft.EntityFrameworkCore;
using backend.Data;
using Microsoft.AspNetCore.Http;
using backend;
using backend.Services;
using backend.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

DotNetEnv.Env.Load();

builder.Configuration.AddEnvironmentVariables();

builder.Logging.AddConsole();
DotNetEnv.Env.Load();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrWhiteSpace(jwtSecretKey))
{
    throw new Exception("La variable d'environnement JWT_SECRET_KEY n'est pas définie !");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PolytechPresence",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PolytechPresenceAPI",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        options.JsonSerializerOptions.MaxDepth = 64;
    });

var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
if (string.IsNullOrWhiteSpace(frontendUrl))
{
    throw new Exception("La variable d'environnement FRONTEND_URL n'est pas définie !");
}

// Configuration CORS stricte
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue",
        policy => policy.WithOrigins(frontendUrl)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
});

// Enregistrement des services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddSingleton<TimerService>();
builder.Services.AddSingleton<AdminTokenService>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ICookieEncryptionService, CookieEncryptionService>();

// Enregistrement du DbContext avec PostgreSQL.
// EnableRetryOnFailure : réessaie automatiquement les échecs transitoires
// (micro-coupures réseau/DB) au lieu de renvoyer un 500 immédiat.
// Pool de connexions PostgreSQL dimensionné explicitement (le défaut Npgsql = 100,
// non documenté auparavant). MaxPoolSize doit rester <= max_connections de PostgreSQL.
var dbConnString = builder.Configuration.GetConnectionString("DefaultConnection");
var npgsqlCsb = new NpgsqlConnectionStringBuilder(dbConnString)
{
    MaxPoolSize = 100,
    MinPoolSize = 5,
    Timeout = 15,           // attente max pour obtenir une connexion (s)
    CommandTimeout = 30,
};
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        npgsqlCsb.ConnectionString,
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null)));

// IHttpClientFactory : évite de créer un HttpClient par import (épuisement de sockets).
builder.Services.AddHttpClient();

// Services d'arrière-plan
builder.Services.AddHostedService<RateLimitCleanupService>();
builder.Services.AddHostedService<EmailDispatcherService>();

builder.Services.AddControllers();

builder.Services.AddSingleton(new HttpClient(new HttpClientHandler
{
    AllowAutoRedirect = true,
    AutomaticDecompression = System.Net.DecompressionMethods.All
})
{
    Timeout = TimeSpan.FromMinutes(5)
});

var app = builder.Build();

// Force l'instanciation du TimerService au démarrage
app.Services.GetRequiredService<TimerService>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    ApplyMigrations(db, app.Logger);

    var adminStudentNumber = Environment.GetEnvironmentVariable("ADMIN_BASE_STUDENT_NUMBER") ?? "";
    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_BASE_PASSWORD") ?? "";

    if (!db.Users.Any(u => u.IsAdmin) && !string.IsNullOrWhiteSpace(adminStudentNumber) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var adminUser = new backend.Models.User
        {
            Name = "Admin",
            Firstname = "Super",
            StudentNumber = adminStudentNumber,
            Email = "admin@polytechpresence.local",
            Year = "ADMIN",
            IsAdmin = true,
            IsDelegate = false,
            PasswordHash = passwordService.HashPassword(adminPassword),
            Signature = ""
        };
        db.Users.Add(adminUser);
        db.SaveChanges();
    }

    static async Task SyncIdentitySequenceAsync(ApplicationDbContext dbContext, string tableName)
    {
        var sql = tableName switch
        {
            "Sessions" => """
                SELECT setval(
                    pg_get_serial_sequence('"Sessions"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Sessions"), 0) + 1,
                    false
                );
                """,
            "Users" => """
                SELECT setval(
                    pg_get_serial_sequence('"Users"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Users"), 0) + 1,
                    false
                );
                """,
            "Attendances" => """
                SELECT setval(
                    pg_get_serial_sequence('"Attendances"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Attendances"), 0) + 1,
                    false
                );
                """,
            "IcsLinks" => """
                SELECT setval(
                    pg_get_serial_sequence('"IcsLinks"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "IcsLinks"), 0) + 1,
                    false
                );
                """,
            "Specializations" => """
                SELECT setval(
                    pg_get_serial_sequence('"Specializations"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Specializations"), 0) + 1,
                    false
                );
                """,
            "SessionSentToUsers" => """
                SELECT setval(
                    pg_get_serial_sequence('"SessionSentToUsers"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "SessionSentToUsers"), 0) + 1,
                    false
                );
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Table non prise en charge pour la synchronisation de séquence.")
        };

        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    // Applique les migrations EF au démarrage. Gère le cas d'une base créée
    // historiquement via EnsureCreated (donc sans table __EFMigrationsHistory) :
    // on "adopte" les migrations en marquant comme déjà appliquées toutes celles
    // correspondant au schéma existant, puis Migrate() applique les nouvelles.
    static void ApplyMigrations(ApplicationDbContext dbContext, ILogger logger)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        if (!dbCreator.Exists())
        {
            // Base neuve : Migrate() crée la base et applique TOUTES les migrations.
            logger.LogInformation("Base absente : création + application de toutes les migrations.");
            dbContext.Database.Migrate();
            return;
        }

        var historyRepo = dbContext.GetService<IHistoryRepository>();
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        var all = migrationsAssembly.Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        // Migrations déjà enregistrées (table d'historique absente OU vide => aucune).
        var applied = historyRepo.Exists()
            ? historyRepo.GetAppliedMigrations().Select(m => m.MigrationId).ToHashSet()
            : new HashSet<string>();

        // Schéma déjà présent (base historiquement créée via EnsureCreated) mais AUCUNE
        // migration enregistrée : on "adopte" les migrations en marquant comme déjà appliquées
        // toutes celles correspondant au schéma existant (toutes sauf la dernière : indexes +
        // drop TargetGroup), puis Migrate() applique la dernière. Sans ça, Migrate rejouerait
        // InitialCreate sur des tables existantes ("relation already exists").
        if (applied.Count == 0 && TableExists(dbContext, "Sessions"))
        {
            var toBaseline = all.Take(all.Count - 1).ToList();

            logger.LogWarning(
                "Base sans historique EF (héritée d'EnsureCreated) : baseline de {Count} migration(s), puis application des nouvelles.",
                toBaseline.Count);

            using var tx = dbContext.Database.BeginTransaction();
            dbContext.Database.ExecuteSqlRaw(historyRepo.GetCreateIfNotExistsScript());
            foreach (var id in toBaseline)
            {
                dbContext.Database.ExecuteSqlRaw(historyRepo.GetInsertScript(
                    new HistoryRow(id, ProductInfo.GetVersion())));
            }
            tx.Commit();
        }

        var pending = dbContext.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
            logger.LogInformation("Application de {Count} migration(s) en attente : {Migrations}", pending.Count, string.Join(", ", pending));
        dbContext.Database.Migrate();
    }

    static bool TableExists(ApplicationDbContext dbContext, string table)
    {
        var conn = dbContext.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT to_regclass('public.\"{table}\"') IS NOT NULL";
            return cmd.ExecuteScalar() is bool b && b;
        }
        finally
        {
            if (wasClosed) conn.Close();
        }
    }

    await SyncIdentitySequenceAsync(db, "Sessions");
    await SyncIdentitySequenceAsync(db, "Users");
    await SyncIdentitySequenceAsync(db, "Attendances");
    await SyncIdentitySequenceAsync(db, "IcsLinks");
    await SyncIdentitySequenceAsync(db, "Specializations");
    await SyncIdentitySequenceAsync(db, "SessionSentToUsers");
}



app.UseRequestLogging();

// Configuration des headers de sécurité
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

app.UseCors("AllowVue");
app.UseSession();
app.UseRouting();

// Authentification et autorisation
app.UseAuthentication();
app.UseAuthorization();

// Notre middleware personnalisé après l'authentification
app.UseAuthMiddleware();

app.MapControllers();

app.Run();
