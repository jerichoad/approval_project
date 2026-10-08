using backendApproval.Contracts;
using backendApproval.Errors;
using backendApproval.Observability;
using Microsoft.AspNetCore.Mvc;

namespace backendApproval.Controllers;

public abstract class BaseApiController(ILogger logger) : ControllerBase
{
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (AppException ex)
        {
            logger.LogWarning(ex, "{Code} ({Status}) on {Path}: {Message}",
                ex.Code, ex.StatusCode, Request.Path, ex.Message);
            return ErrorResult(ex.StatusCode, ex.Code, ex.Message, ex.Extensions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Path}", Request.Path);
            return ErrorResult(StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "Terjadi kesalahan tak terduga di server.");
        }
    }

    protected IActionResult Envelope<T>(T data, int statusCode = StatusCodes.Status200OK, string? location = null)
    {
        if (location is not null) Response.Headers.Location = location;
        return new ObjectResult(ApiResponse<T>.Success(data)) { StatusCode = statusCode };
    }

    private IActionResult ErrorResult(int statusCode, string code, string message,
                                      Dictionary<string, object?>? extensions = null)
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.Header] as string;
        var body = ApiErrorResponse.Error(code, message, correlationId, extensions);
        return new ObjectResult(body) { StatusCode = statusCode };
    }
}
