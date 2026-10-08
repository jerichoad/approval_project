using backendApproval.Contracts;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendApproval.Controllers;

[ApiController]
[Route("api/approvals")]
public sealed class ApprovalsController(AccessRequestService service, ILogger<ApprovalsController> logger)
    : BaseApiController(logger)
{
    [HttpGet("inbox")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AccessRequestSummaryResponse>>>(StatusCodes.Status200OK)]
    public Task<IActionResult> Inbox(CancellationToken ct) => ExecuteAsync(async () =>
    {
        var inbox = await service.InboxAsync(ct);
        return Envelope(inbox);
    });
}
