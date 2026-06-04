using System.Text.Encodings.Web;
using System.Text.Json;

namespace CarRentalSystem.Middleware
{
    public static class RateLimitResponseWriter
    {
        public static async Task WriteHtmlAsync(
            HttpContext context,
            string title,
            string message,
            TimeSpan retryAfter)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
            context.Response.ContentType = "text/html; charset=utf-8";

            var encodedTitle = HtmlEncoder.Default.Encode(title);
            var encodedMessage = HtmlEncoder.Default.Encode(message);
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));

            await context.Response.WriteAsync($$"""
                <!doctype html>
                <html lang="vi">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>{{encodedTitle}}</title>
                    <style>
                        body{margin:0;font-family:system-ui,-apple-system,Segoe UI,sans-serif;background:#f8fafc;color:#111827}
                        main{min-height:100vh;display:grid;place-items:center;padding:24px}
                        section{max-width:560px;width:100%;background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:28px;box-shadow:0 12px 30px rgba(15,23,42,.08)}
                        h1{font-size:1.35rem;margin:0 0 10px}
                        p{line-height:1.55;margin:0 0 18px;color:#374151}
                        a{display:inline-block;color:#0f766e;font-weight:600;text-decoration:none}
                    </style>
                </head>
                <body>
                    <main>
                        <section>
                            <h1>{{encodedTitle}}</h1>
                            <p>{{encodedMessage}}</p>
                            <p>Vui long thu lai sau {{minutes}} phut.</p>
                            <a href="/">Ve trang chu</a>
                        </section>
                    </main>
                </body>
                </html>
                """);
        }

        public static async Task WriteJsonAsync(
            HttpContext context,
            object payload,
            TimeSpan retryAfter)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
