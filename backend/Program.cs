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

var builder = WebApplication.CreateBuilder(args);

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

var databasePath = Environment.GetEnvironmentVariable("STORAGE_PATH");
if (string.IsNullOrWhiteSpace(databasePath))
{
    throw new Exception("La variable d'environnement STORAGE_PATH n'est pas définie !");
}
if (!Directory.Exists(databasePath))
{
    Directory.CreateDirectory(databasePath);
}
databasePath = System.IO.Path.Combine(databasePath, "polytechpresence.db");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

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

// Services d'arrière-plan
builder.Services.AddHostedService<RateLimitCleanupService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
    db.Database.EnsureCreated();

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
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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
