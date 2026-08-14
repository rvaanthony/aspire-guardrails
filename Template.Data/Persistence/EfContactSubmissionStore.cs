#if (UseEfCore)
using Microsoft.EntityFrameworkCore;
using Template.Common.Interfaces;
using Template.Common.Models;
using Template.Data.Persistence.Entities;

namespace Template.Data.Persistence;

public sealed class EfContactSubmissionStore : IContactSubmissionStore
{
    private readonly AppDbContext _db;

    public EfContactSubmissionStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> AddAsync(ContactSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var record = new ContactSubmissionRecord
        {
            Id = Guid.CreateVersion7(),
            Topic = submission.Topic,
            Name = submission.Name,
            Email = submission.Email,
            Organization = submission.Organization,
            Message = submission.Message,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = "new"
        };

        _db.ContactSubmissions.Add(record);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return record.Id.ToString("N");
    }

    public async Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        return await _db.ContactSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Take(take)
            .Select(x => new ContactSubmission
            {
                Id = x.Id.ToString("N"),
                Topic = x.Topic,
                Name = x.Name,
                Email = x.Email,
                Organization = x.Organization,
                Message = x.Message,
                CreatedUtc = x.CreatedUtc,
                Status = x.Status
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
#endif
