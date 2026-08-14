namespace Template.Client.Middleware;

/// <summary>
/// Adds security headers to every response. CSP has no 'unsafe-inline' for
/// scripts or styles — keep views free of inline &lt;script&gt; blocks and
/// style="" attributes so the strict policy costs nothing. Add external hosts
/// (CDNs, font providers) to the relevant directive as the app adopts them.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        // Indexer assignment (not Append): UseStatusCodePagesWithReExecute and
        // UseExceptionHandler re-run this middleware for the same response, and
        // Append would emit each header twice on 404/error pages.
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.XFrameOptions = "DENY";
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

        var csp = string.Join("; ",
        [
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self'",
            "font-src 'self'",
            "img-src 'self' data:",
            "connect-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'",
            "upgrade-insecure-requests"
        ]);
        response.Headers.ContentSecurityPolicy = csp;

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder) =>
        builder.UseMiddleware<SecurityHeadersMiddleware>();
}
