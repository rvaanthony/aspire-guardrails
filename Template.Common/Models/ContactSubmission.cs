namespace Template.Common.Models;

public sealed class ContactSubmission
{
    public string? Id { get; init; }
    public required string Topic { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Organization { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string Status { get; init; } = "new";
}
