namespace Template.Client.Seo;

public interface ISitemapProvider
{
    IReadOnlyList<SitemapEntry> GetEntries();
}

public sealed record SitemapEntry(string Path, double? Priority = null);
