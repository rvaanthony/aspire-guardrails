namespace Template.Worker;

public sealed class ContactDigestOptions
{
    public const string SectionName = "ContactDigest";

    /// <summary>
    /// How long to wait between digest ticks. Bound from configuration as a
    /// timespan string, e.g. "00:15:00". Must be greater than zero — validated at
    /// startup in Program.cs rather than clamped here, so a nonsense value fails
    /// the deploy instead of silently becoming something else.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>How many of the newest submissions each tick looks at.</summary>
    public int Take { get; init; } = 5;
}
