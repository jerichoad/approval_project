using backendApproval.Contracts;
using backendApproval.Domain;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendApproval.Controllers;

[ApiController]
[Route("api/access-requests")]
public sealed class AccessRequestsController(AccessRequestService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccessRequestRequest body, CancellationToken ct)
    {
        var result = await service.CreateAsync(body, ct);
        var dto = await service.GetDetailAsync(result.Request.Id, ct);
        return result.IsNew
            ? CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto)
            : Ok(dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccessRequestSummaryResponse>>> List(
        [FromQuery] string? scope, CancellationToken ct)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "mine" : scope.Trim().ToLowerInvariant();
        return normalizedScope switch
        {
            "mine" => Ok(await service.ListMineAsync(ct)),
            "all" => Ok(await service.ListAllAsync(ct)),
            _ => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid scope",
                detail: "Query param 'scope' harus bernilai 'mine' atau 'all'.")
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccessRequestDetailResponse>> GetById(Guid id, CancellationToken ct)
    {
        var detail = await service.GetDetailAsync(id, ct);
        return Ok(detail);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AccessRequestDetailResponse>> Approve(
        Guid id, [FromBody] ApproveRequest body, CancellationToken ct)
    {
        await service.DecideAsync(id, Decision.Approve, body.ExpectedVersion!.Value, null, ct);
        var detail = await service.GetDetailAsync(id, ct);
        return Ok(detail);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AccessRequestDetailResponse>> Reject(
        Guid id, [FromBody] RejectRequest body, CancellationToken ct)
    {
        await service.DecideAsync(id, Decision.Reject, body.ExpectedVersion!.Value, body.Reason, ct);
        var detail = await service.GetDetailAsync(id, ct);
        return Ok(detail);
    }
}
