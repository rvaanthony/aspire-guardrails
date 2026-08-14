#if (!UseEfCore)
using System.Globalization;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Template.Common.Interfaces;
using Template.Common.Models;
using Template.Data.Entities.v1.Table;
using Template.Data.Storage;

namespace Template.Data.Stores;

public sealed class ContactSubmissionStore : IContactSubmissionStore
{
    private const string PartitionKeyValue = "contact";

    private readonly TableClient _table;

    public ContactSubmissionStore(TableServiceClient tableServiceClient, IOptions<StorageOptions> options)
    {
        _table = tableServiceClient.GetTableClient(options.Value.ContactSubmissionsTableName);
    }

    public async Task<string> AddAsync(ContactSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var createdUtc = DateTimeOffset.UtcNow;
        var rowKey = $"{createdUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}";

        var entity = new ContactSubmissionEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = rowKey,
            Topic = submission.Topic,
            Name = submission.Name,
            Email = submission.Email,
            Organization = submission.Organization,
            Message = submission.Message,
            CreatedUtc = createdUtc,
            Status = "new"
        };

        await _table.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        return rowKey;
    }

    public async Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        var results = new List<ContactSubmission>();
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKeyValue}");

        await foreach (var entity in _table.QueryAsync<ContactSubmissionEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            results.Add(ToModel(entity));
        }

        return results
            .OrderByDescending(s => s.Id, StringComparer.Ordinal)
            .Take(take)
            .ToArray();
    }

    private static ContactSubmission ToModel(ContactSubmissionEntity entity) => new()
    {
        Id = entity.RowKey,
        Topic = entity.Topic,
        Name = entity.Name,
        Email = entity.Email,
        Organization = entity.Organization,
        Message = entity.Message,
        CreatedUtc = entity.CreatedUtc,
        Status = entity.Status
    };
}
#endif
