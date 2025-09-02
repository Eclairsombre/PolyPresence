using backend.Services;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backend.Middleware
{
    /// <summary>
    /// Middleware qui vérifie si l'utilisateur est connecté avant de traiter la requête
    /// </summary>
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthMiddleware> _logger;

        public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, AdminTokenService tokenService)
        {
            var publicPaths = new List<string>
            {
                "/api/User/login",
                "/api/User/register",
                "/api/User/verify-token",
                "/api/User/send-register-link",
                "/api/User/reset-password",
                "/api/User/reset-password-request",
                "/api/User/search",
                "/api/User/IsUserAdmin",
                "/api/Status"
            };

            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (
                publicPaths.Any(p => path.StartsWith(p.ToLower())) ||
                path.StartsWith("/api/user/search/") ||
                path.StartsWith("/api/user/isuseradmin/")))
            {
                await _next(context);
                return;
            }

            var adminToken = context.Request.Headers["Admin-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(adminToken))
            {
                var adminUserId = tokenService.ValidateToken(adminToken);
                if (adminUserId.HasValue)
                {
                    var adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == adminUserId.Value && u.IsAdmin);
                    if (adminUser != null)
                    {
                        _logger.LogInformation("Accès administrateur autorisé pour {StudentNumber} à {Path}", adminUser.StudentNumber, context.Request.Path);
                        await _next(context);
                        return;
                    }
                }
            }

            var studentId = context.Request.Headers["X-User-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(studentId))
            {
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.StudentNumber == studentId);
                if (user != null)
                {
                    _logger.LogInformation("Accès utilisateur autorisé pour {StudentNumber} à {Path}", studentId, context.Request.Path);
                    await _next(context);
                    return;
                }
            }

            _logger.LogWarning("Tentative d'accès non autorisé à {Path}", context.Request.Path);
            context.Response.StatusCode = 401; // Unauthorized
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Vous devez être connecté pour accéder à cette ressource" }));
        }
    }

    // Extension pour faciliter l'ajout du middleware dans Program.cs
    public static class AuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthMiddleware>();
        }
    }
}
