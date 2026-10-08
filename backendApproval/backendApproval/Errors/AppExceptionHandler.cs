using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace backendApproval.Errors;

public sealed class AppExceptionHandler(IProblemDetailsService problemDetails,
                                        ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException app)
        {
            logger.LogError(exception, "Unhandled exception on {Path}", ctx.Request.Path);
            return false;
        }

        logger.LogWarning("{Code} ({Status}) on {Path}: {Message}",
            app.Code, app.StatusCode, ctx.Request.Path, app.Message);

        var problem = new ProblemDetails
        {
            Status = app.StatusCode,
            Title = app.Code,
            Detail = app.Message,
            Instance = ctx.Request.Path
        };
        problem.Extensions["code"] = app.Code;
        foreach (var (key, value) in app.Extensions) problem.Extensions[key] = value;

        ctx.Response.StatusCode = app.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
