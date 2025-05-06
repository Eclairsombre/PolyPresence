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

var databasePath = Environment.GetEnvironmentVariable("STORAGE_PATH");
if (string.IsNullOrWhiteSpace(databasePath))
{
    throw new Exception("La variable d'environnement STORAGE_PATH n'est pas définie !");
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

builder.Services.AddSingleton(new HttpClient(new HttpClientHandler
{
    AllowAutoRedirect = true,
    AutomaticDecompression = System.Net.DecompressionMethods.All
})
{
    Timeout = TimeSpan.FromMinutes(5)
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Activer CORS
app.UseCors("AllowVue");

app.MapGet("/", () => "CORS Proxy Server - Utilisez /proxy?url=VOTRE_URL");

app.MapMethods("/proxy", new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD" },
    async (HttpContext context, HttpClient httpClient) =>
{
    string targetUrl = context.Request.Query["url"];

    if (string.IsNullOrEmpty(targetUrl))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Paramètre 'url' manquant");
        return;
    }

    try
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(targetUrl)
        };

        // Copier les headers de la requête originale
        foreach (var header in context.Request.Headers)
        {
            // Ignorer les headers qui sont gérés par HttpClient ou posent problème
            if (!ShouldSkipHeader(header.Key))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Copier le body de la requête si nécessaire
        if (context.Request.ContentLength > 0)
        {
            // Préserver le corps de la requête pour le lire
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var bodyContent = await reader.ReadToEndAsync();
            requestMessage.Content = new StringContent(bodyContent);

            // Réinitialiser la position du corps de la requête
            context.Request.Body.Position = 0;

            // Copier le content-type s'il existe
            if (context.Request.ContentType != null)
            {
                requestMessage.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        var response = await httpClient.SendAsync(requestMessage);

        // Copier le status code
        context.Response.StatusCode = (int)response.StatusCode;

        // Copier les headers de la réponse
        foreach (var header in response.Headers)
        {
            if (!ShouldSkipHeader(header.Key) && !context.Response.Headers.ContainsKey(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        // Copier les headers du content si présents
        if (response.Content?.Headers != null)
        {
            foreach (var header in response.Content.Headers)
            {
                if (header.Key != "Transfer-Encoding" && !context.Response.Headers.ContainsKey(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }
        }

        // Ajouter les headers CORS explicitement
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH, HEAD";
        context.Response.Headers["Access-Control-Allow-Headers"] = "*";
        context.Response.Headers["Access-Control-Expose-Headers"] = "*";
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";

        // Au lieu de copier directement le stream, lire tout le contenu et l'écrire
        // Cela évitera les problèmes avec le chunked encoding
        var responseContent = await response.Content.ReadAsStringAsync();
        await context.Response.WriteAsync(responseContent);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Erreur du proxy: {ex.Message}");
    }
});


bool ShouldSkipHeader(string headerName)
{
    string[] headersToSkip = {
        "Host", "Connection", "Content-Length", "Origin",
        "Referer", "Accept-Encoding", "Accept-Language", "X-Forwarded-For",
        "X-Forwarded-Proto", "X-Forwarded-Host", "Via", "Transfer-Encoding"
    };

    return Array.Exists(headersToSkip, h => h.Equals(headerName, StringComparison.OrdinalIgnoreCase));
}


// Activer la session avant UseRouting
app.UseSession();

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
