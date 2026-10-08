using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController(AppDbContext db, ILogger<ApplicationsController> logger)
    : BaseApiController(logger)
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ApplicationResponse>>>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll(CancellationToken ct) => ExecuteAsync(async () =>
    {
        var apps = await db.Applications.AsNoTracking()
            .Include(a => a.SystemOwner)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
        return Envelope<IReadOnlyList<ApplicationResponse>>(
            apps.Select(AccessRequestService.ToApplication).ToList());
    });
}
