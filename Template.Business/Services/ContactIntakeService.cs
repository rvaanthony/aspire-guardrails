using Microsoft.Extensions.Logging;
using Template.Common.Interfaces;
using Template.Common.Models;

namespace Template.Business.Services;

/// <summary>
/// Example vertical-slice use case: validate-and-persist a contact form submission.
/// Controllers call this; they never talk to stores directly.
/// </summary>
public sealed class ContactIntakeService
{
    private readonly IContactSubmissionStore _store;
    private readonly ILogger<ContactIntakeService> _logger;

    public ContactIntakeService(IContactSubmissionStore store, ILogger<ContactIntakeService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<string> SubmitAsync(ContactSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var id = await _store.AddAsync(submission, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Contact submission {SubmissionId} stored.", id);
        return id;
    }

    public Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default) =>
        _store.ListNewestAsync(take, cancellationToken);
}

