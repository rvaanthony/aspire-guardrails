#if (!UseEfCore)
using Azure;
using Azure.Data.Tables;

namespace Template.Data.Entities.v1.Table;

/// <summary>
/// Contact form submission stored in Azure Table Storage.
/// PartitionKey: "contact". RowKey: yyyyMMddHHmmss-Guid for chronological order.
/// </summary>
public sealed class ContactSubmissionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Topic { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public string Status { get; set; } = "new";
}
#endif
