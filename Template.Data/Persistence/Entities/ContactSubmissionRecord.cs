#if (UseEfCore)
namespace Template.Data.Persistence.Entities;

public sealed class ContactSubmissionRecord
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public string Status { get; set; } = "new";
}
#endif
