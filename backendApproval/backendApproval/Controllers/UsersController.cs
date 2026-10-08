using backendApproval.Auth;
using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Controllers;

[ApiController]
[Route("api")]
public sealed class UsersController(AppDbContext db, CurrentUser currentUser, ILogger<UsersController> logger)
    : BaseApiController(logger)
{
    [HttpGet("users")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<UserSummaryResponse>>>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll(CancellationToken ct) => ExecuteAsync(async () =>
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserSummaryResponse(u.Id, u.Email, u.DisplayName))
            .ToListAsync(ct);
        return Envelope<IReadOnlyList<UserSummaryResponse>>(users);
    });

    [HttpGet("me")]
    [ProducesResponseType<ApiResponse<MeResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetMe(CancellationToken ct) => ExecuteAsync(async () =>
    {
        var me = currentUser.User;
        var hasDirectReports = await db.Users.AsNoTracking().AnyAsync(u => u.ManagerId == me.Id, ct);
        var ownedApps = await db.Applications.AsNoTracking()
            .Include(a => a.SystemOwner)
            .Where(a => a.SystemOwnerId == me.Id)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return Envelope(new MeResponse(
            me.Id,
            me.Email,
            me.DisplayName,
            me.IsAuditor,
            hasDirectReports,
            ownedApps.Select(AccessRequestService.ToApplication).ToList()));
    });
}
