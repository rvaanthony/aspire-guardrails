using Template.Common.Interfaces;
using Template.Common.Models;

namespace Template.Business.Tests.Fakes;

// Deliberately duplicated in Template.Business.Tests and Template.Client.Tests.
// Test projects must not reference each other, so the alternatives are a shared
// test-support project — a new project in every generated app, with a layer-map
// entry, an ArchitectureTests entry and a coverage floor row, for thirty lines —
// or a cross-project Compile link, which couples by path and is invisible to the
// layer map. Both cost more than the copy. Change one, change the other.

public sealed class FakeContactSubmissionStore : IContactSubmissionStore
{
    public List<ContactSubmission> Added { get; } = [];

    public Task<string> AddAsync(ContactSubmission submission, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        Added.Add(new ContactSubmission
        {
            Id = id,
            Topic = submission.Topic,
            Name = submission.Name,
            Email = submission.Email,
            Organization = submission.Organization,
            Message = submission.Message,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = "new"
        });
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContactSubmission>>(Added
            .OrderByDescending(x => x.CreatedUtc)
            .Take(take)
            .ToArray());
}
