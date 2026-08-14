#if (UseSentry)
using Sentry;
#endif

namespace Template.Client.Middleware;

/// <summary>
/// Catches unhandled exceptions: returns JSON for API-like requests with a safe
/// message and correlation id; rethrows for HTML so UseExceptionHandler renders
/// the branded error page. Never includes exception details in production.
/// </summary>
public sealed class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context) ?? "unknown";
            _logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
#if (UseSentry)
            SentrySdk.CaptureException(ex, scope => scope.SetTag("correlation_id", correlationId));
#endif

            var isApiRequest = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                || context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

            if (isApiRequest)
            {
                await WriteJsonErrorResponseAsync(context, correlationId);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task WriteJsonErrorResponseAsync(HttpContext context, string correlationId)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var message = _environment.IsDevelopment()
            ? "An internal server error occurred."
            : "An internal server error occurred. Please try again later.";
        await context.Response.WriteAsJsonAsync(new { message, correlationId }, context.RequestAborted);
    }
}

public static class ErrorHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandler(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ErrorHandlerMiddleware>();
}
