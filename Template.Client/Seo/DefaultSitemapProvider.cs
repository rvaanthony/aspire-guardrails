namespace Template.Client.Seo;

/// <summary>
/// Lists the example pages this starter ships with. Add an entry here for
/// every publicly indexable route as you add pages.
/// </summary>
public sealed class DefaultSitemapProvider : ISitemapProvider
{
    public IReadOnlyList<SitemapEntry> GetEntries() =>
    [
        new SitemapEntry("/", Priority: 1.0),
        new SitemapEntry("/privacy", Priority: 0.3),
        new SitemapEntry("/contact", Priority: 0.5)
    ];
}
