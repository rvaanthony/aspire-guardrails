using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Template.Client.Models;
using Template.Client.Tests.Fakes;
using Template.Common.Interfaces;

namespace Template.Client.Tests.Controllers;

public sealed class ContactControllerTests
{
#if (!UseAzureAd)
    [Fact]
    public async Task Post_HappyPath_StoresSubmissionAndRedirects()
    {
        var store = new FakeContactSubmissionStore();
        await using var factory = CreateFactory(store);
        using var client = CreateClient(factory);
        var token = await GetAntiForgeryTokenAsync(client);

        using var response = await client.PostAsync("/contact", FormContent(token, new()
        {
            ["Topic"] = ContactFormModel.TopicGeneral,
            ["Name"] = "Jamie Rivera",
            ["Email"] = "jamie@example.com",
            ["Message"] = "We would like to request support for our community."
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(store.Added);
        Assert.Equal("jamie@example.com", store.Added[0].Email);
    }

    [Fact]
    public async Task Post_HoneypotTripped_ReturnsSuccessWithoutStoring()
    {
        var store = new FakeContactSubmissionStore();
        await using var factory = CreateFactory(store);
        using var client = CreateClient(factory);
        var token = await GetAntiForgeryTokenAsync(client);

        using var response = await client.PostAsync("/contact", FormContent(token, new()
        {
            ["Topic"] = ContactFormModel.TopicGeneral,
            ["Name"] = "Bot",
            ["Email"] = "bot@example.com",
            ["Message"] = "Buy cheap watches now, click here for a great deal.",
            ["Website"] = "http://spam.example.com"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task Post_ExceedsRateLimit_Returns429()
    {
        var store = new FakeContactSubmissionStore();
        await using var factory = CreateFactory(store);
        using var client = CreateClient(factory);
        var token = await GetAntiForgeryTokenAsync(client);

        HttpResponseMessage response = null!;
        for (var i = 0; i < 7; i++)
        {
            response = await client.PostAsync("/contact", FormContent(token, new()
            {
                ["Topic"] = ContactFormModel.TopicGeneral,
                ["Name"] = $"Sender {i}",
                ["Email"] = $"sender{i}@example.com",
                ["Message"] = "We would like to request support for our community."
            }));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Api_ListNewest_ReturnsJson()
    {
        var store = new FakeContactSubmissionStore();
        await using var factory = CreateFactory(store);
        using var client = CreateClient(factory);
        var token = await GetAntiForgeryTokenAsync(client);
        using var post = await client.PostAsync("/contact", FormContent(token, new()
        {
            ["Topic"] = ContactFormModel.TopicGeneral,
            ["Name"] = "Jamie Rivera",
            ["Email"] = "jamie@example.com",
            ["Message"] = "We would like to request support for our community."
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var response = await client.GetAsync("/api/v1/contact");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(ContactFormModel.TopicGeneral, body, StringComparison.Ordinal);
        // The listing is anonymous-accessible — it must never leak submitter PII.
        Assert.DoesNotContain("jamie@example.com", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Jamie Rivera", body, StringComparison.Ordinal);
    }
#endif

    private static WebApplicationFactory<Program> CreateFactory(IContactSubmissionStore store) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => services.AddSingleton(store));
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static FormUrlEncodedContent FormContent(string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(fields);
    }

    private static async Task<string> GetAntiForgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/contact");
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"");
        return match.Groups["token"].Value;
    }
}
