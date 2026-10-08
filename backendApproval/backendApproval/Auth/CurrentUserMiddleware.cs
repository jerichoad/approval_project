using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Observability;
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
            var correlationId = ctx.Items[CorrelationIdMiddleware.Header] as string;
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var errorBody = ApiErrorResponse.Error(
                code: "UNAUTHENTICATED",
                message: "Kirim header X-User-Email dengan salah satu demo user.",
                correlationId: correlationId);
            await ctx.Response.WriteAsJsonAsync(errorBody);
            return;
        }

        currentUser.Set(user);
        using (logger.BeginScope(new Dictionary<string, object> { ["UserEmail"] = user.Email }))
        {
            await next(ctx);
        }
    }
}
