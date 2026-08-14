#if (UseEfCore)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Template.Data.Persistence;

/// <summary>
/// Applies the schema at startup. Uses real migrations once the project has
/// any; falls back to EnsureCreated for a brand-new project that has none yet.
/// See docs/adr/0004-ef-migration-growth-path.md.
/// </summary>
public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    public DatabaseInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Database migrated.");
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "Database schema created via EnsureCreated (no migrations found). Run " +
                "'dotnet ef migrations add InitialCreate --project Template.Data --startup-project Template.Data' " +
                "to switch to migrations before this schema needs to change.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
#endif
