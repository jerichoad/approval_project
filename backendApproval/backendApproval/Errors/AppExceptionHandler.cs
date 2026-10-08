using backendApproval.Contracts;
using backendApproval.Observability;
using Microsoft.AspNetCore.Diagnostics;

namespace backendApproval.Errors;

// Fallback untuk exception yang lolos dari BaseApiController.ExecuteAsync (mis. exception di middleware
// sebelum controller, atau action yang belum dibungkus ExecuteAsync). Setiap controller action yang
// sudah memakai ExecuteAsync menangani AppException sendiri lewat try-catch dan tidak akan sampai ke sini.
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        var correlationId = ctx.Items[CorrelationIdMiddleware.Header] as string;

        ApiErrorResponse body;
        int statusCode;

        if (exception is AppException app)
        {
            logger.LogWarning(exception, "{Code} ({Status}) on {Path}: {Message}",
                app.Code, app.StatusCode, ctx.Request.Path, app.Message);
            statusCode = app.StatusCode;
            body = ApiErrorResponse.Error(app.Code, app.Message, correlationId, app.Extensions);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception on {Path}", ctx.Request.Path);
            statusCode = StatusCodes.Status500InternalServerError;
            body = ApiErrorResponse.Error("INTERNAL_ERROR", "Terjadi kesalahan tak terduga di server.", correlationId);
        }

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(body, ct);
        return true;
    }
}
