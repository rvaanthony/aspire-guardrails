using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Template.Client.Seo;

public static class SeoExtensions
{
    public static IServiceCollection AddSeo(this IServiceCollection services)
    {
        services
            .AddOptions<SeoOptions>()
            .BindConfiguration(SeoOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<ISitemapProvider, DefaultSitemapProvider>();

        return services;
    }

    // Minimal API endpoints — deliberately outside MVC so they stay reachable
    // for crawlers even under --auth azuread: the global AuthorizeFilter is
    // registered via AddControllersWithViews and only ever runs for MVC
    // controller actions, never for endpoint-routed minimal APIs like these.
    public static WebApplication MapSeoEndpoints(this WebApplication app)
    {
        app.MapGet("/robots.txt", (IOptions<SeoOptions> options, IWebHostEnvironment env) =>
        {
            var seo = options.Value;
            // Fail-closed: indexing requires BOTH the explicit opt-in AND a
            // real Production environment, so a misconfigured or staging
            // deploy never accidentally invites crawlers.
            if (!seo.AllowIndexing || !env.IsProduction())
            {
                return Results.Text("User-agent: *\nDisallow: /\n", "text/plain");
            }

            return Results.Text("User-agent: *\nAllow: /\nSitemap: /sitemap.xml\n", "text/plain");
        });

        app.MapGet("/sitemap.xml", (IOptions<SeoOptions> options, ISitemapProvider sitemapProvider, HttpContext context) =>
        {
            var seo = options.Value;
            var baseUrl = string.IsNullOrWhiteSpace(seo.CanonicalHost)
                ? $"{context.Request.Scheme}://{context.Request.Host}"
                : $"https://{seo.CanonicalHost}";

            var urls = string.Concat(sitemapProvider.GetEntries().Select(entry =>
                $"<url><loc>{baseUrl}{entry.Path}</loc>{(entry.Priority is { } priority ? $"<priority>{priority:0.0}</priority>" : string.Empty)}</url>"));
            var xml = $"""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">{urls}</urlset>""";

            return Results.Text(xml, "application/xml");
        });

        return app;
    }
}
