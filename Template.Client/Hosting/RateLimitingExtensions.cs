using System.Net;
using System.Threading.RateLimiting;

namespace Template.Client.Hosting;

/// <summary>
/// Global IP-partitioned rate limiter. Anonymous abuse-prone POST endpoints
/// (contact forms, signups, ...) get a strict per-minute bucket; everything
/// else gets a much looser bucket plus a concurrency cap. Static assets and
/// health checks are excluded entirely.
/// </summary>
internal static class RateLimitingExtensions
{
    private const int StrictPostPermitLimit = 5;
    private const int DefaultPermitLimit = 120;
    private const int ConcurrentRequestLimit = 20;

    // Path prefixes of anonymous write endpoints that deserve the strict
    // bucket. Add more as abuse-prone POSTs appear (signups, webhooks, …).
    private static readonly string[] StrictPostPaths = ["/contact"];

    public static IServiceCollection AddTemplateRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many requests. Please try again in a minute." },
                    cancellationToken);
            };

            var minuteLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (IsExcludedPath(context.Request.Path))
                {
                    return RateLimitPartition.GetNoLimiter("_");
                }

                var isStrictPost = HttpMethods.IsPost(context.Request.Method)
                    && StrictPostPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
                var permitLimit = isStrictPost ? StrictPostPermitLimit : DefaultPermitLimit;

                // Fold the permit limit into the key so the (IP, bucket) pair gets its own
                // limiter — otherwise the options factory locks in whichever limit the
                // FIRST request from that IP happened to need for the rest of the window.
                var key = $"m{permitLimit}:{GetPartitionKey(context)}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1)
                });
            });

            var concurrencyLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (IsExcludedPath(context.Request.Path))
                {
                    return RateLimitPartition.GetNoLimiter("_");
                }

                var key = $"c:{GetPartitionKey(context)}";
                return RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = ConcurrentRequestLimit,
                    QueueLimit = 0
                });
            });

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(minuteLimiter, concurrencyLimiter);
        });

        return services;
    }

    private static bool IsExcludedPath(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);

    private static string GetPartitionKey(HttpContext context)
    {
        // Trust only the RIGHTMOST X-Forwarded-For entry: Azure App Service's
        // front end APPENDS the real client IP, so the last entry is
        // platform-controlled while earlier entries are attacker-supplied.
        // (Leftmost would let a caller rotate fake IPs per request and bypass
        // the strict POST bucket entirely.)
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var normalized = NormalizeAddress(forwarded.Split(',')[^1].Trim());
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    // App Service appends the client IP as "ip:port"; IPv6 may arrive bracketed
    // ("[2001:db8::1]:443") or bare. Returns null for unparseable values so the
    // caller falls back to the connection's remote address.
    private static string? NormalizeAddress(string value)
    {
        if (IPAddress.TryParse(value, out var address))
        {
            return address.ToString();
        }

        if (IPEndPoint.TryParse(value, out var endpoint))
        {
            return endpoint.Address.ToString();
        }

        return null;
    }
}
