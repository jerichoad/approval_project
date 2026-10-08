namespace backendApproval.Errors;

public abstract class AppException(int statusCode, string code, string message,
                                   Dictionary<string, object?>? extensions = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public Dictionary<string, object?> Extensions { get; } = extensions ?? new();
}

public sealed class NotFoundException(string code, string message)
    : AppException(StatusCodes.Status404NotFound, code, message);

public sealed class ForbiddenException(string code, string message)
    : AppException(StatusCodes.Status403Forbidden, code, message);

public sealed class ConflictException(string code, string message, Dictionary<string, object?>? ext = null)
    : AppException(StatusCodes.Status409Conflict, code, message, ext);

public sealed class BusinessRuleException(string code, string message)
    : AppException(StatusCodes.Status422UnprocessableEntity, code, message);

public sealed class BadRequestException(string code, string message)
    : AppException(StatusCodes.Status400BadRequest, code, message);
