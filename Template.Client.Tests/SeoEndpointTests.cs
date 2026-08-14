using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Template.Client.Tests;

/// <summary>
/// These deliberately run unguarded (no #if UseAzureAd) — the point of
/// mapping SEO endpoints as minimal APIs is that they stay reachable
/// regardless of the auth variant, which these prove.
/// </summary>
public sealed class SeoEndpointTests
{
    [Fact]
    public async Task Robots_DisallowsByDefault()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Disallow: /", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sitemap_ReturnsUrlset()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/sitemap.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<urlset", body, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
}
