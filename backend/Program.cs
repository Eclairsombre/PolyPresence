using Microsoft.EntityFrameworkCore;
using backend.Data;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
// Ajouter avant de construire l'application
builder.Logging.AddConsole();
// Ajouter la prise en charge des sessions
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
        options.JsonSerializerOptions.MaxDepth = 64; // (optionnel, augmente la profondeur max)
    });
DotNetEnv.Env.Load();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
if (string.IsNullOrWhiteSpace(frontendUrl))
{
    throw new Exception("La variable d'environnement FRONTEND_URL n'est pas définie !");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue",
        policy => policy.WithOrigins(frontendUrl)
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Activer CORS
app.UseCors("AllowVue");

// Activer la session avant UseRouting
app.UseSession();

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Vos autres configurations...

app.Run();