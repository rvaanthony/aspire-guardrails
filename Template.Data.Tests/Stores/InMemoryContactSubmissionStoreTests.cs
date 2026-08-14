using Template.Common.Models;
using Template.Data.Stores;

namespace Template.Data.Tests.Stores;

/// <summary>
/// The store the <c>Testing</c> host environment substitutes in, which is what lets
/// the HTTP boundary tests boot with no container at all. It is worth testing
/// directly for that reason: if it misbehaves, every suite that depends on it fails
/// somewhere far away from the cause.
/// <para>
/// No Docker, no fixture — these are ordinary unit tests.
/// </para>
/// </summary>
public sealed class InMemoryContactSubmissionStoreTests
{
    [Fact]
    public async Task AddAsync_AssignsAnIdAndOwnsTheServerSideFields()
    {
        var store = new InMemoryContactSubmissionStore();
        var before = DateTimeOffset.UtcNow;

        var id = await store.AddAsync(new ContactSubmission
        {
            Topic = "general",
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Organization = "Analytical Engine",
            Message = "Body.",
            // Supplied and ignored, matching the Tables and EF stores: the store
            // decides the identity, the instant and the status.
            Id = "caller-supplied",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            Status = "closed"
        });

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.NotEqual("caller-supplied", id);

        var stored = Assert.Single(await store.ListNewestAsync(10));
        Assert.Equal(id, stored.Id);
        Assert.Equal("general", stored.Topic);
        Assert.Equal("Ada Lovelace", stored.Name);
        Assert.Equal("ada@example.com", stored.Email);
        Assert.Equal("Analytical Engine", stored.Organization);
        Assert.Equal("Body.", stored.Message);
        Assert.Equal("new", stored.Status);
        Assert.InRange(stored.CreatedUtc, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task AddAsync_RejectsNull()
    {
        var store = new InMemoryContactSubmissionStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.AddAsync(null!));
    }

    [Fact]
    public async Task AddAsync_GivesEverySubmissionADistinctId()
    {
        var store = new InMemoryContactSubmissionStore();

        var ids = new List<string>();
        for (var i = 0; i < 25; i++)
        {
            ids.Add(await store.AddAsync(NewSubmission($"topic-{i}")));
        }

        // Distinct ids are what stop one submission overwriting another in the
        // backing dictionary — a silent data loss rather than an error.
        Assert.Equal(25, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(25, (await store.ListNewestAsync(100)).Count);
    }

    [Fact]
    public async Task ListNewestAsync_ReturnsNewestFirst()
    {
        var store = new InMemoryContactSubmissionStore();
        for (var i = 0; i < 5; i++)
        {
            await store.AddAsync(NewSubmission($"topic-{i}"));
        }

        var results = await store.ListNewestAsync(10);

        // Asserted as a non-increasing invariant rather than an exact sequence.
        // CreatedUtc comes from UtcNow, so several adds inside one clock tick can
        // tie; an exact-order assertion would fail on timing rather than on the
        // ordering being wrong.
        var timestamps = results.Select(x => x.CreatedUtc).ToArray();
        Assert.Equal(timestamps.OrderByDescending(x => x), timestamps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task ListNewestAsync_ReturnsEmptyForNonPositiveTake(int take)
    {
        var store = new InMemoryContactSubmissionStore();
        await store.AddAsync(NewSubmission("general"));

        // int.MinValue is here because the implementation clamps with Math.Max
        // rather than branching, and Take() throws on a negative count.
        Assert.Empty(await store.ListNewestAsync(take));
    }

    [Fact]
    public async Task ListNewestAsync_TakesAtMostTheRequestedCount()
    {
        var store = new InMemoryContactSubmissionStore();
        for (var i = 0; i < 4; i++)
        {
            await store.AddAsync(NewSubmission($"topic-{i}"));
        }

        Assert.Equal(2, (await store.ListNewestAsync(2)).Count);
        Assert.Equal(4, (await store.ListNewestAsync(50)).Count);
    }

    [Fact]
    public async Task ListNewestAsync_ReturnsEmptyWhenNothingHasBeenStored()
    {
        var store = new InMemoryContactSubmissionStore();

        Assert.Empty(await store.ListNewestAsync(10));
    }

    private static ContactSubmission NewSubmission(string topic) => new()
    {
        Topic = topic,
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Message = "Body."
    };
}
