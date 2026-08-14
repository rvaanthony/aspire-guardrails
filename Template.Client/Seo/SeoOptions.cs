namespace Template.Client.Seo;

public sealed class SeoOptions
{
    public const string SectionName = "Seo";

    /// <summary>
    /// Fail-closed: crawlers are blocked unless explicitly opted in here, and
    /// even then only outside Development/Staging — see MapSeoEndpoints.
    /// </summary>
    public bool AllowIndexing { get; init; }

    /// <summary>
    /// Bare host (e.g. "example.com"), no scheme. Falls back to the current
    /// request's own host when unset.
    /// </summary>
    public string? CanonicalHost { get; init; }
}
