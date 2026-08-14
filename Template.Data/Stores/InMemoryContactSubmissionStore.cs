using System.Collections.Concurrent;
using Template.Common.Interfaces;
using Template.Common.Models;

namespace Template.Data.Stores;

/// <summary>
/// Process-local store used by the Testing host environment so
/// WebApplicationFactory boots without Docker/Azurite/Postgres.
/// </summary>
public sealed class InMemoryContactSubmissionStore : IContactSubmissionStore
{
    private readonly ConcurrentDictionary<string, ContactSubmission> _items = new(StringComparer.Ordinal);

    public Task<string> AddAsync(ContactSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var id = Guid.NewGuid().ToString("N");
        var stored = new ContactSubmission
        {
            Id = id,
            Topic = submission.Topic,
            Name = submission.Name,
            Email = submission.Email,
            Organization = submission.Organization,
            Message = submission.Message,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = "new"
        };
        _items[id] = stored;
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ContactSubmission> items = _items.Values
            .OrderByDescending(x => x.CreatedUtc)
            .Take(Math.Max(0, take))
            .ToArray();
        return Task.FromResult(items);
    }
}
