using System.Diagnostics;
using System.Text.RegularExpressions;

namespace backend.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        // Masque les valeurs sensibles (JWT du SSE, tokens de signature) dans la query string
        // pour ne JAMAIS les écrire en clair dans les logs.
        private static readonly Regex SensitiveQueryParams =
            new("(?i)((?:access_token|token)=)[^&]*", RegexOptions.Compiled);

        private static readonly Regex SensitivePathSegments =
            new("(?i)(/prof-signature/)[^/]+", RegexOptions.Compiled);

        private static string RedactPath(string? path) =>
            string.IsNullOrEmpty(path) ? "" : SensitivePathSegments.Replace(path, "$1***");

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        private static string RedactQuery(string? query) =>
            string.IsNullOrEmpty(query) ? "" : SensitiveQueryParams.Replace(query, "$1***");

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation($"REQ: {context.Request.Method} {RedactPath(context.Request.Path.Value)}{RedactQuery(context.Request.QueryString.Value)}");

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var statusCode = context.Response.StatusCode;
                _logger.LogInformation($"RES: {statusCode} {context.Request.Method} {RedactPath(context.Request.Path.Value)} ({sw.ElapsedMilliseconds}ms)");
            }
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
