#if (!UseEfCore)
using System.ComponentModel.DataAnnotations;

namespace Template.Data.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    [StringLength(63, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{2,62}$")]
    public string ContactSubmissionsTableName { get; init; } = "ContactSubmissions";

    [Required]
    [StringLength(63, MinimumLength = 3)]
    [RegularExpression("^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$")]
    public string PrivateContainerName { get; init; } = "app-private";

    [Required]
    [StringLength(63, MinimumLength = 3)]
    [RegularExpression("^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$")]
    public string JobsQueueName { get; init; } = "app-jobs";
}
#endif
