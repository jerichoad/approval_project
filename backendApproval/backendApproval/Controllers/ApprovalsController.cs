using backendApproval.Contracts;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendApproval.Controllers;

[ApiController]
[Route("api/approvals")]
public sealed class ApprovalsController(AccessRequestService service) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<AccessRequestSummaryResponse>>> Inbox(CancellationToken ct)
    {
        var inbox = await service.InboxAsync(ct);
        return Ok(inbox);
    }
}
