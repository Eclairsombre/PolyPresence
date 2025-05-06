using Microsoft.AspNetCore.Http;
using System.Net.Http;

namespace backend
{
    public static class ProxyEndpoint
    {
        public static void MapProxyEndpoint(this WebApplication app)
        {
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
                        if (!ShouldSkipHeader(header.Key))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                        }
                    }

                    // Copier le body de la requête si nécessaire
                    if (context.Request.ContentLength > 0)
                    {
                        context.Request.EnableBuffering();
                        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                        var bodyContent = await reader.ReadToEndAsync();
                        requestMessage.Content = new StringContent(bodyContent);
                        context.Request.Body.Position = 0;
                        if (context.Request.ContentType != null)
                        {
                            requestMessage.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                        }
                    }

                    var response = await httpClient.SendAsync(requestMessage);
                    context.Response.StatusCode = (int)response.StatusCode;

                    foreach (var header in response.Headers)
                    {
                        if (!ShouldSkipHeader(header.Key) && !context.Response.Headers.ContainsKey(header.Key))
                        {
                            context.Response.Headers[header.Key] = header.Value.ToArray();
                        }
                    }

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

                    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH, HEAD";
                    context.Response.Headers["Access-Control-Allow-Headers"] = "*";
                    context.Response.Headers["Access-Control-Expose-Headers"] = "*";
                    context.Response.Headers["Access-Control-Allow-Credentials"] = "true";

                    var responseContent = await response.Content.ReadAsStringAsync();
                    await context.Response.WriteAsync(responseContent);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync($"Erreur du proxy: {ex.Message}");
                }
            });
        }

        private static bool ShouldSkipHeader(string headerName)
        {
            string[] headersToSkip = {
                "Host", "Connection", "Content-Length", "Origin",
                "Referer", "Accept-Encoding", "Accept-Language", "X-Forwarded-For",
                "X-Forwarded-Proto", "X-Forwarded-Host", "Via", "Transfer-Encoding"
            };
            return Array.Exists(headersToSkip, h => h.Equals(headerName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
