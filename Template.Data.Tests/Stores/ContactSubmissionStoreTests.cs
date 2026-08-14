#if (!UseEfCore)
using System.Text.RegularExpressions;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Template.Common.Models;
using Template.Data.Entities.v1.Table;
using Template.Data.Storage;
using Template.Data.Stores;
using Template.Data.Tests.Azurite;

namespace Template.Data.Tests.Stores;

[Collection(AzuriteCollection.Name)]
public sealed class ContactSubmissionStoreTests
{
    private const string PartitionKeyValue = "contact";

    private readonly AzuriteContainerFixture _fixture;

    public ContactSubmissionStoreTests(AzuriteContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AddAsync_PersistsEveryFieldAndStampsTheServerSideValues()
    {
        var (store, table) = await CreateStoreAsync();
        var before = DateTimeOffset.UtcNow;

        var rowKey = await store.AddAsync(new ContactSubmission
        {
            Topic = "partnership",
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Organization = "Analytical Engine",
            Message = "Every field should survive the round trip.",
            // Deliberately supplied and deliberately ignored: the store owns these
            // three, and a caller must not be able to backdate a submission or
            // declare it already handled.
            Id = "caller-supplied",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            Status = "closed"
        });

        var stored = await table.GetEntityAsync<ContactSubmissionEntity>(PartitionKeyValue, rowKey);
        var entity = stored.Value;

        Assert.Equal("partnership", entity.Topic);
        Assert.Equal("Ada Lovelace", entity.Name);
        Assert.Equal("ada@example.com", entity.Email);
        Assert.Equal("Analytical Engine", entity.Organization);
        Assert.Equal("Every field should survive the round trip.", entity.Message);
        Assert.Equal("new", entity.Status);
        Assert.InRange(entity.CreatedUtc, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal(PartitionKeyValue, entity.PartitionKey);
    }

    [Fact]
    public async Task AddAsync_ReturnsARowKeyThatSortsChronologically()
    {
        var (store, _) = await CreateStoreAsync();

        var rowKey = await store.AddAsync(NewSubmission("general"));

        // yyyyMMddHHmmss-<32 hex>. The timestamp prefix is what makes an ordinal
        // sort on the row key a chronological sort, which is the whole reason
        // ListNewestAsync can order without reading a second column.
        Assert.Matches(new Regex(@"^\d{14}-[0-9a-f]{32}$", RegexOptions.None, TimeSpan.FromSeconds(1)), rowKey);
    }

    [Fact]
    public async Task AddAsync_RejectsNull()
    {
        var (store, _) = await CreateStoreAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.AddAsync(null!));
    }

    [Fact]
    public async Task ListNewestAsync_OrdersByRowKeyDescending()
    {
        var (store, table) = await CreateStoreAsync();

        // Rows are inserted directly with controlled keys rather than through
        // AddAsync. AddAsync stamps the prefix at second resolution, so three quick
        // calls would land in the same second and their relative order would come
        // down to a random GUID — a test that passes or fails on timing rather than
        // on the ordering logic being asserted here.
        await InsertAsync(table, "20260101000001-aaaa", "oldest");
        await InsertAsync(table, "20260101000003-cccc", "newest");
        await InsertAsync(table, "20260101000002-bbbb", "middle");

        var results = await store.ListNewestAsync(10);

        Assert.Equal(["newest", "middle", "oldest"], results.Select(x => x.Topic));
    }

    [Fact]
    public async Task ListNewestAsync_RespectsTake()
    {
        var (store, table) = await CreateStoreAsync();
        await InsertAsync(table, "20260101000001-aaaa", "oldest");
        await InsertAsync(table, "20260101000002-bbbb", "middle");
        await InsertAsync(table, "20260101000003-cccc", "newest");

        var results = await store.ListNewestAsync(2);

        Assert.Equal(["newest", "middle"], results.Select(x => x.Topic));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListNewestAsync_ReturnsEmptyForNonPositiveTakeWithoutQuerying(int take)
    {
        // Points at a table that was never created. If the guard clause were removed
        // this would fail with a TableNotFound error rather than returning empty,
        // which is what proves the short-circuit happens before any I/O.
        var options = new StorageOptions { ContactSubmissionsTableName = NewTableName() };
        var store = new ContactSubmissionStore(
            new TableServiceClient(_fixture.ConnectionString), Options.Create(options));

        var results = await store.ListNewestAsync(take);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ListNewestAsync_MapsEveryFieldBackOntoTheModel()
    {
        var (store, _) = await CreateStoreAsync();
        var rowKey = await store.AddAsync(new ContactSubmission
        {
            Topic = "support",
            Name = "Grace Hopper",
            Email = "grace@example.com",
            Organization = "Harvard",
            Message = "Round trip through the model, not just the entity."
        });

        var result = Assert.Single(await store.ListNewestAsync(10));

        Assert.Equal(rowKey, result.Id);
        Assert.Equal("support", result.Topic);
        Assert.Equal("Grace Hopper", result.Name);
        Assert.Equal("grace@example.com", result.Email);
        Assert.Equal("Harvard", result.Organization);
        Assert.Equal("Round trip through the model, not just the entity.", result.Message);
        Assert.Equal("new", result.Status);
        Assert.NotEqual(default, result.CreatedUtc);
    }

    [Fact]
    public async Task ListNewestAsync_ToleratesAnAbsentOrganization()
    {
        var (store, _) = await CreateStoreAsync();
        await store.AddAsync(new ContactSubmission
        {
            Topic = "general",
            Name = "Alan Turing",
            Email = "alan@example.com",
            Organization = null,
            Message = "Organization is the one optional field."
        });

        var result = Assert.Single(await store.ListNewestAsync(10));

        Assert.Null(result.Organization);
    }

    private static ContactSubmission NewSubmission(string topic) => new()
    {
        Topic = topic,
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Message = "Body."
    };

    // A fresh table per test. The Azurite container is shared across the whole
    // collection, so a shared table name would let one test's rows appear in
    // another's ListNewestAsync results.
    //
    // Letter-led and alphanumeric because that is all Azure Table naming permits —
    // the same rule StorageOptions' own RegularExpression enforces.
    private static string NewTableName() => $"cst{Guid.NewGuid():N}";

    private async Task<(ContactSubmissionStore Store, TableClient Table)> CreateStoreAsync()
    {
        var options = new StorageOptions { ContactSubmissionsTableName = NewTableName() };
        var serviceClient = new TableServiceClient(_fixture.ConnectionString);
        var table = serviceClient.GetTableClient(options.ContactSubmissionsTableName);

        // The store does not create its own table; StorageInitializerHostedService
        // does that at startup. Creating it here keeps that split honest rather than
        // hiding it behind a store that quietly provisions.
        await table.CreateIfNotExistsAsync();

        return (new ContactSubmissionStore(serviceClient, Options.Create(options)), table);
    }

    private static Task InsertAsync(TableClient table, string rowKey, string topic) =>
        table.AddEntityAsync(new ContactSubmissionEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = rowKey,
            Topic = topic,
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Message = "Body.",
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = "new"
        });
}
#endif
