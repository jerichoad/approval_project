using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationResponse>>> GetAll(CancellationToken ct)
    {
        var apps = await db.Applications.AsNoTracking()
            .Include(a => a.SystemOwner)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
        return Ok(apps.Select(AccessRequestService.ToApplication).ToList());
    }
}
