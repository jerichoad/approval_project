using backendApproval.Contracts;
using backendApproval.Domain;
using backendApproval.Errors;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendApproval.Controllers;

[ApiController]
[Route("api/access-requests")]
public sealed class AccessRequestsController(AccessRequestService service, ILogger<AccessRequestsController> logger)
    : BaseApiController(logger)
{
    [HttpPost]
    [ProducesResponseType<ApiResponse<AccessRequestDetailResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<AccessRequestDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Create([FromBody] CreateAccessRequestRequest body, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await service.CreateAsync(body, ct);
            var dto = await service.GetDetailAsync(result.Request.Id, ct);
            return result.IsNew
                ? Envelope(dto, StatusCodes.Status201Created,
                    Url.Action(nameof(GetById), new { id = dto.Id }))
                : Envelope(dto);
        });

    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AccessRequestSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> List([FromQuery] string? scope, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "mine" : scope.Trim().ToLowerInvariant();
        var rows = normalizedScope switch
        {
            "mine" => await service.ListMineAsync(ct),
            "all" => await service.ListAllAsync(ct),
            _ => throw new BadRequestException("INVALID_SCOPE",
                "Query param 'scope' harus bernilai 'mine' atau 'all'.")
        };
        return Envelope(rows);
    });

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<AccessRequestDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var detail = await service.GetDetailAsync(id, ct);
        return Envelope(detail);
    });

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ApiResponse<AccessRequestDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest body, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await service.DecideAsync(id, Decision.Approve, body.ExpectedVersion!.Value, null, ct);
            var detail = await service.GetDetailAsync(id, ct);
            return Envelope(detail);
        });

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ApiResponse<AccessRequestDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest body, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await service.DecideAsync(id, Decision.Reject, body.ExpectedVersion!.Value, body.Reason, ct);
            var detail = await service.GetDetailAsync(id, ct);
            return Envelope(detail);
        });
}
