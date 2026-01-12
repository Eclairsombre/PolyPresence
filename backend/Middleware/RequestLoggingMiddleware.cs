using System.Diagnostics;

namespace backend.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation($"REQ: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");

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
