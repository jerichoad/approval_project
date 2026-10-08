using backendApproval.Auth;
using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Controllers;

[ApiController]
[Route("api")]
public sealed class UsersController(AppDbContext db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserSummaryResponse(u.Id, u.Email, u.DisplayName))
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken ct)
    {
        var me = currentUser.User;
        var hasDirectReports = await db.Users.AsNoTracking().AnyAsync(u => u.ManagerId == me.Id, ct);
        var ownedApps = await db.Applications.AsNoTracking()
            .Include(a => a.SystemOwner)
            .Where(a => a.SystemOwnerId == me.Id)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return Ok(new MeResponse(
            me.Id,
            me.Email,
            me.DisplayName,
            me.IsAuditor,
            hasDirectReports,
            ownedApps.Select(AccessRequestService.ToApplication).ToList()));
    }
}
