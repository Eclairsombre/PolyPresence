using backend.Services;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;

namespace backend.Middleware
{
    /// <summary>
    /// Middleware qui vérifie l'authentification JWT avant de traiter la requête
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

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, IJwtService jwtService, IRateLimitService rateLimitService)
        {
            var publicPaths = new HashSet<string>
            {
                "/api/User/login",
                "/api/User/forgot-password",
                "/api/User/reset-password",
                "/api/Status",
                "/api/User/refresh-token",
                "/api/User/search"
            };

            var path = context.Request.Path.Value?.ToLowerInvariant();

            if (path != null && publicPaths.Any(p => path.StartsWith(p.ToLowerInvariant())))
            {
                await _next(context);
                return;
            }

            var userIdFromToken = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdFromToken) && !rateLimitService.IsApiCallAllowed(userIdFromToken))
            {
                _logger.LogWarning("Rate limit exceeded for user {UserId} on path {Path}", userIdFromToken, path);
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Rate limit exceeded" }));
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                _logger.LogWarning("Missing or invalid Authorization header for path {Path}", path);
                await UnauthorizedResponse(context, "Token d'accès requis");
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var principal = await jwtService.ValidateTokenAsync(token);
            if (principal == null)
            {
                _logger.LogWarning("Invalid JWT token for path {Path}", path);
                await UnauthorizedResponse(context, "Token d'accès invalide ou expiré");
                return;
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID in token for path {Path}", path);
                await UnauthorizedResponse(context, "Token invalide");
                return;
            }

            var user = await dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found in database", userId);
                await UnauthorizedResponse(context, "Utilisateur introuvable");
                return;
            }

            context.User = principal;
            context.Items["User"] = user;
            context.Items["UserId"] = userId;

            _logger.LogDebug("User {UserId} authenticated successfully for path {Path}", userId, path);

            await _next(context);
        }

        private async Task UnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = true,
                message = message,
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
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
