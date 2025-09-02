using Microsoft.EntityFrameworkCore;
using backend.Data;
using Microsoft.AspNetCore.Http;
using backend;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        options.JsonSerializerOptions.MaxDepth = 64;
    });
DotNetEnv.Env.Load();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue",
        policy => policy.WithOrigins(frontendUrl)
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TimerService>();
builder.Services.AddSingleton<AdminTokenService>();

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
        var adminUser = new backend.Models.User
        {
            Name = "Admin",
            Firstname = "Super",
            StudentNumber = adminStudentNumber,
            Email = "admin@polytechpresence.local",
            Year = "ADMIN",
            IsAdmin = true,
            IsDelegate = false,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
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

app.UseCors("AllowVue");

app.UseSession();

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
