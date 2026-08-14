using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Template.Business.Services;

namespace Template.Worker;

/// <summary>
/// Example background worker — periodically logs a summary of recent contact
/// submissions. The pattern (PeriodicTimer + a scoped service resolved per
/// tick) is what matters here; replace the body with your own periodic or
/// queue-driven work.
/// </summary>
public sealed class ContactDigestWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContactDigestOptions _options;
    private readonly ILogger<ContactDigestWorker> _logger;

    public ContactDigestWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ContactDigestOptions> options,
        ILogger<ContactDigestWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The interval comes from options rather than a constant so that a test
        // can drive several ticks in milliseconds. A hard-coded 15 minutes makes
        // everything below unobservable, including the catch block.
        using var timer = new PeriodicTimer(_options.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var contactIntake = scope.ServiceProvider.GetRequiredService<ContactIntakeService>();
                var recent = await contactIntake.ListNewestAsync(_options.Take, stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Contact digest: {Count} recent submission(s).", recent.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bad tick (e.g. a transient DB blip) shouldn't take down the
                // whole worker — log it and let the timer try again next interval.
                _logger.LogError(ex, "Contact digest tick failed; will retry next interval.");
            }
        }
    }
}
