using Template.Common.Models;

namespace Template.Common.Interfaces;

public interface IContactSubmissionStore
{
    /// <summary>Persists a new submission. The store assigns Id/CreatedUtc/Status.</summary>
    Task<string> AddAsync(ContactSubmission submission, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default);
}
