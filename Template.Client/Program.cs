#if (UseAzureAd)
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
#endif
using Template.Business.Extensions;
using Template.Client.Hosting;
using Template.Client.Middleware;
using Template.Client.Seo;
using Template.Data.Extensions;

var builder = WebApplication.CreateBuilder(args);

#if (UseSentry)
builder.WebHost.UseSentry();
#endif

builder.AddServiceDefaults();
builder.AddDataInfrastructure();
builder.AddBusinessServices();

builder.Services.AddTemplateRateLimiting();
builder.Services.AddSeo();
builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
#if (UseAzureAd)

// Cookie sign-in with Microsoft Entra ID. The global filter means every
// controller requires an authenticated user; opt out per-action with
// [AllowAnonymous] (the error/404/contact pages already do).
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();
#else
builder.Services.AddControllersWithViews();
#endif

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapSeoEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseCorrelationId();
app.UseSecurityHeaders();

app.UseRouting();
app.UseRateLimiter();
#if (UseAzureAd)
app.UseAuthentication();
app.UseAuthorization();
#endif
app.UseErrorHandler();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
#if (UseAzureAd)

app.MapRazorPages()
   .WithStaticAssets();
#endif

app.Run();

// Exposes the entry point to WebApplicationFactory-based tests.
public partial class Program
{
}
