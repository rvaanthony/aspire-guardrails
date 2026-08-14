using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Template.Client.Tests;

/// <summary>
/// HTTP-boundary checks that run against the real pipeline (middleware,
/// routing, rate limiter) in the "Testing" environment, which skips the Azure
/// storage clients so no Docker/Azurite is required.
/// </summary>
public sealed class SmokeTests
{
#if (!UseAzureAd)
    [Fact]
    public async Task Home_ReturnsSuccess()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

#endif
    [Fact]
    public async Task Health_ReturnsSuccess()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/this-page-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeadersAndCorrelationId()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);

        // /health sits before routing in the middleware order, so it carries
        // the same headers and works whether or not sign-in is configured.
        using var response = await client.GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.NotEmpty(response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.NotEmpty(response.Headers.GetValues("X-Correlation-Id").Single());
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
