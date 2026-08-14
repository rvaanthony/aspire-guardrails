namespace Template.Client.Middleware;

/// <summary>
/// Assigns a per-request correlation id (X-Correlation-Id from the client if
/// present, otherwise a new id) for error responses and logging.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }

    public static string? GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder) =>
        builder.UseMiddleware<CorrelationIdMiddleware>();
}
