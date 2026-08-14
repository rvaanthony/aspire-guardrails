using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Template.Business.Services;

namespace Template.Client.Controllers.Api;

/// <summary>
/// Example versioned JSON API surface under /api/v1. Prefer ActionResult&lt;T&gt;
/// so return types stay visible to architecture scans and OpenAPI generators.
/// </summary>
[ApiController]
[Route("api/v1/contact")]
public sealed class ContactApiController : ControllerBase
{
    private readonly ContactIntakeService _contactIntakeService;

    public ContactApiController(ContactIntakeService contactIntakeService)
    {
        _contactIntakeService = contactIntakeService;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContactSubmissionDto>>> ListNewest(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var items = await _contactIntakeService.ListNewestAsync(take, cancellationToken).ConfigureAwait(false);
        var dto = items.Select(x => new ContactSubmissionDto(
            x.Id,
            x.Topic,
            x.Organization,
            x.CreatedUtc,
            x.Status)).ToArray();
        return Ok(dto);
    }
}

// Name/Email intentionally excluded — this endpoint is anonymous-accessible,
// so the listing must not leak submitter PII to unauthenticated callers.
public sealed record ContactSubmissionDto(
    string? Id,
    string Topic,
    string? Organization,
    DateTimeOffset CreatedUtc,
    string Status);
