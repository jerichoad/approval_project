using backendApproval.Data;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Auth;

public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AnonymousPaths = ["/api/users"];

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, CurrentUser currentUser,
                                  ILogger<CurrentUserMiddleware> logger)
    {
        var path = ctx.Request.Path;
        if (!path.StartsWithSegments("/api") || AnonymousPaths.Any(p => path.StartsWithSegments(p)))
        {
            await next(ctx);
            return;
        }

        var email = ctx.Request.Headers["X-User-Email"].ToString().Trim().ToLowerInvariant();
        var user = email.Length == 0
            ? null
            : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            logger.LogWarning("Unauthenticated request to {Path}", path);
            await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unknown or missing user",
                detail: "Kirim header X-User-Email dengan salah satu demo user.",
                extensions: new Dictionary<string, object?> { ["code"] = "UNAUTHENTICATED" })
                .ExecuteAsync(ctx);
            return;
        }

        currentUser.Set(user);
        using (logger.BeginScope(new Dictionary<string, object> { ["UserEmail"] = user.Email }))
        {
            await next(ctx);
        }
    }
}
