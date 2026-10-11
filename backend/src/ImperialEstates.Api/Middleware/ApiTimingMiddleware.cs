using System.Diagnostics;
using System.Globalization;

namespace ImperialEstates.Api.Middleware;

public sealed class ApiTimingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var timer = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Server-Timing"] =
                $"app;dur={timer.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)}";
            // Financial and personal data must never enter a shared proxy/browser cache.
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next(context);
    }
}
