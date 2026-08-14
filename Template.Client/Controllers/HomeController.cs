using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Template.Business.Services;
using Template.Client.Models;
using Template.Common.Models;

namespace Template.Client.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ContactIntakeService _contactIntakeService;

    public HomeController(ILogger<HomeController> logger, ContactIntakeService contactIntakeService)
    {
        _logger = logger;
        _contactIntakeService = contactIntakeService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Example vertical-slice endpoint: form → Business → Data store.
    // AllowAnonymous so the sample works with --auth azuread without a login wall.
    [AllowAnonymous]
    [HttpGet("/contact")]
    public IActionResult Contact(string? topic)
    {
        var model = new ContactFormModel();
        if (ContactFormModel.IsKnownTopic(topic))
        {
            model.Topic = topic!;
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost("/contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactFormModel model, CancellationToken cancellationToken)
    {
        // Honeypot tripped: pretend success so bots learn nothing, store nothing.
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            TempData["ContactSent"] = true;
            return RedirectToAction(nameof(Contact));
        }

        if (!ContactFormModel.IsKnownTopic(model.Topic))
        {
            ModelState.AddModelError(nameof(model.Topic), "Please choose what this is about.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _contactIntakeService.SubmitAsync(
                new ContactSubmission
                {
                    Topic = model.Topic,
                    Name = model.Name,
                    Email = model.Email,
                    Organization = model.Organization,
                    Message = model.Message
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store contact submission.");
            ModelState.AddModelError(string.Empty, "We couldn't send this — please try again shortly.");
            return View(model);
        }

        TempData["ContactSent"] = true;
        return RedirectToAction(nameof(Contact));
    }

    // Target of UseStatusCodePagesWithReExecute: re-renders unmatched routes
    // (and explicit 404s) with a branded page while preserving the status code.
    [AllowAnonymous]
    [Route("/not-found")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult PageNotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
