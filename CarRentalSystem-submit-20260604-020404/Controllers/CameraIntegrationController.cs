using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("integrations/camera")]
    public class CameraIntegrationController : ControllerBase
    {
        private readonly ICameraAnprService _cameraAnprService;

        public CameraIntegrationController(ICameraAnprService cameraAnprService)
        {
            _cameraAnprService = cameraAnprService;
        }

        [HttpPost("anpr-events")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ReceiveAnprEvent(CancellationToken cancellationToken)
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            Request.Body.Position = 0;

            var result = await _cameraAnprService.HandleWebhookAsync(new CameraAnprWebhookEnvelope(
                Request.Headers["X-Camera-Code"].ToString(),
                Request.Headers["X-Camera-Timestamp"].ToString(),
                Request.Headers["X-Camera-Nonce"].ToString(),
                Request.Headers["X-Camera-Signature"].ToString(),
                rawBody), cancellationToken);

            var response = new
            {
                message = result.Message,
                eventId = result.CameraGateEventID,
                matchStatus = result.MatchStatus
            };

            return result.Outcome switch
            {
                CameraAnprWebhookOutcome.Accepted => Ok(response),
                CameraAnprWebhookOutcome.Duplicate => Ok(response),
                CameraAnprWebhookOutcome.BadRequest => BadRequest(response),
                CameraAnprWebhookOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
                _ => Unauthorized(response)
            };
        }
    }
}
