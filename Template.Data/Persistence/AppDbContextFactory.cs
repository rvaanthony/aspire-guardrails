#if (UseEfCore)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Template.Data.Persistence;

/// <summary>
/// Lets `dotnet ef` construct <see cref="AppDbContext"/> at design time (e.g.
/// `dotnet ef migrations add`) without running the full Aspire host — Aspire
/// normally injects the connection string via service discovery at runtime,
/// which isn't available here, so this factory falls back to a local
/// placeholder. Only `dotnet ef database update` needs that connection string
/// to actually resolve; `migrations add` just needs the provider and the model.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
#if (UsePostgres)
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__template")
            ?? "Host=localhost;Database=template;Username=postgres;Password=postgres";
        builder.UseNpgsql(connectionString);
#elif (UseSqlServer)
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__template")
            ?? "Server=localhost;Database=template;Trusted_Connection=True;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString);
#endif
        return new AppDbContext(builder.Options);
    }
}
#endif
