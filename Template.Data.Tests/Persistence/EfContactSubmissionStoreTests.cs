#if (UseEfCore)
using Microsoft.EntityFrameworkCore;
using Template.Common.Models;
using Template.Data.Persistence;
#if (UsePostgres)
using Template.Data.Tests.Postgres;
#elif (UseSqlServer)
using Template.Data.Tests.SqlServer;
#endif

namespace Template.Data.Tests.Persistence;

#if (UsePostgres)
[Collection(PostgresCollection.Name)]
#elif (UseSqlServer)
[Collection(SqlServerCollection.Name)]
#endif
public sealed class EfContactSubmissionStoreTests
{
#if (UsePostgres)
    private readonly PostgresContainerFixture _fixture;

    public EfContactSubmissionStoreTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }
#elif (UseSqlServer)
    private readonly SqlServerContainerFixture _fixture;

    public EfContactSubmissionStoreTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }
#endif

    [Fact]
    public async Task AddAsync_PersistsAndListsNewestFirst()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        var store = new EfContactSubmissionStore(db);

        var firstId = await store.AddAsync(new ContactSubmission
        {
            Topic = "general",
            Name = "Ada",
            Email = "ada@example.com",
            Message = "First message that is long enough."
        });
        var secondId = await store.AddAsync(new ContactSubmission
        {
            Topic = "support",
            Name = "Grace",
            Email = "grace@example.com",
            Message = "Second message that is long enough."
        });

        var newest = await store.ListNewestAsync(10);
        Assert.Equal(2, newest.Count);
        Assert.Equal(secondId, newest[0].Id);
        Assert.Equal(firstId, newest[1].Id);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
#if (UsePostgres)
            .UseNpgsql(_fixture.ConnectionString)
#elif (UseSqlServer)
            .UseSqlServer(_fixture.ConnectionString)
#endif
            .Options;
        return new AppDbContext(options);
    }
}
#endif
