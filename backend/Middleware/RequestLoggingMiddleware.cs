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
            _logger.LogInformation($"REQ: {context.Request.Method} {context.Request.Path}{RedactQuery(context.Request.QueryString.Value)}");

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var statusCode = context.Response.StatusCode;
                _logger.LogInformation($"RES: {statusCode} {context.Request.Method} {context.Request.Path} ({sw.ElapsedMilliseconds}ms)");
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
