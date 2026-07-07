using backend.Services;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace backend.Middleware
{
    /// <summary>
    /// Middleware qui vérifie l'authentification JWT avant de traiter la requête
    /// </summary>
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthMiddleware> _logger;

        // Endpoints "publics par jeton" (lien prof reçu par mail, sans compte connecté)
        // ou auto-validés (SSE : EventSource ne peut pas envoyer d'en-tête Authorization).
        // Ils réalisent EUX-MÊMES leur contrôle d'accès (validation du ProfSignatureToken,
        // ou du access_token JWT transmis en query). On restreint le contournement à des
        // routes EXACTES : l'ancien `Contains("/attendances")` exposait toute la famille
        // /attendances (liste + flux SSE de présences avec données personnelles) au public.
        private static readonly Regex[] TokenValidatedRoutes = new[]
        {
            @"^/api/session/prof-signature/[^/]+$",          // GET/POST signature prof par token
            @"^/api/session/\d+/attendances$",               // liste des présences (page prof, token en en-tête)
            @"^/api/session/\d+/attendances/stream$",        // SSE présences (page prof, token en query)
            @"^/api/session/\d+/attendance-status/[^/]+$",   // bascule de statut (page prof)
            @"^/api/session/\d+/attendance-comment/[^/]+$",  // commentaire (page prof)
            @"^/api/session/current/[^/]+/stream$",          // SSE "cours en cours" étudiant (valide access_token)
        }.Select(p => new Regex(p, RegexOptions.Compiled)).ToArray();

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
                "/api/User/search",
                "/api/User/year",
                "/api/User/have-password",
                "/api/User/send-register-link",
                "/api/User/set-password",
                "/api/User/register",
                "/api/session/prof-signature"
            };

            var requestPath = context.Request.Path.Value?.ToLowerInvariant();

            // Routes publiques par jeton / auto-validées (voir TokenValidatedRoutes) :
            // on ne contourne l'auth JWT que pour ces routes EXACTES. Chaque endpoint
            // concerné valide lui-même le ProfSignatureToken (en-tête/query) ou le access_token.
            if (requestPath != null && TokenValidatedRoutes.Any(r => r.IsMatch(requestPath)))
            {
                await _next(context);
                return;
            }

            if (requestPath != null && requestPath.StartsWith("/api/professor") && context.Request.Method == "GET")
            {
                await _next(context);
                return;
            }

            if (requestPath != null && requestPath.StartsWith("/api/specialization") && context.Request.Method == "GET")
            {
                await _next(context);
                return;
            }

            if (requestPath != null && publicPaths.Any(p => requestPath.StartsWith(p.ToLowerInvariant())))
            {
                await _next(context);
                return;
            }

            var userIdFromToken = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdFromToken) && !rateLimitService.IsApiCallAllowed(userIdFromToken))
            {
                _logger.LogWarning("Rate limit exceeded for user {UserId} on path {Path}", userIdFromToken, requestPath);
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Rate limit exceeded" }));
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                _logger.LogWarning("Missing or invalid Authorization header for path {Path}", requestPath);
                await UnauthorizedResponse(context, "Token d'accès requis");
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var principal = await jwtService.ValidateTokenAsync(token);
            if (principal == null)
            {
                _logger.LogWarning("Invalid JWT token for path {Path}", requestPath);
                await UnauthorizedResponse(context, "Token d'accès invalide ou expiré");
                return;
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("Invalid user ID in token for path {Path}", requestPath);
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

            if (user.IsDeleted)
            {
                _logger.LogWarning("Deleted user {UserId} attempted to authenticate", userId);
                await UnauthorizedResponse(context, "Ce compte a été désactivé.");
                return;
            }

            context.User = principal;
            context.Items["User"] = user;
            context.Items["UserId"] = userId;

            _logger.LogDebug("User {UserId} authenticated successfully for path {Path}", userId, requestPath);

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
