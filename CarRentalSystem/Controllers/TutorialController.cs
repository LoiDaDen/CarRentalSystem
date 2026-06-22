using CarRentalSystem.Models.Tutorial;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers;

[Authorize]
public sealed class TutorialController : Controller
{
    private readonly ITutorialService _tutorials;

    public TutorialController(ITutorialService tutorials)
    {
        _tutorials = tutorials;
    }

    [HttpGet]
    public async Task<IActionResult> GetTutorial(string panel, bool manual = false, CancellationToken cancellationToken = default)
    {
        if (!await _tutorials.CanAccessPanelAsync(User, panel, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        var tutorial = await _tutorials.GetTutorialAsync(User, panel, manual, cancellationToken);
        return tutorial == null ? NoContent() : Json(tutorial);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProgress(
        [FromBody] TutorialProgressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _tutorials.CanAccessPanelAsync(User, request.Panel, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        return await _tutorials.UpdateProgressAsync(User, request, cancellationToken)
            ? Ok(new { updated = true })
            : BadRequest(new { updated = false });
    }
}
