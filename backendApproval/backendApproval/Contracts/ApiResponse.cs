using System.Text.Json.Serialization;

namespace backendApproval.Contracts;

public sealed record ApiResponse<T>
{
    [JsonPropertyName("Status")]
    public string Status { get; init; } = "S";

    [JsonPropertyName("Data")]
    public T? Data { get; init; }

    public static ApiResponse<T> Success(T data) => new() { Status = "S", Data = data };
}

public sealed record ApiErrorData
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("fieldErrors")]
    public Dictionary<string, string[]>? FieldErrors { get; init; }

    [JsonPropertyName("extensions")]
    public Dictionary<string, object?> Extensions { get; init; } = [];
}

public sealed record ApiErrorResponse
{
    [JsonPropertyName("Status")]
    public string Status { get; init; } = "E";

    [JsonPropertyName("Data")]
    public ApiErrorData Data { get; init; } = new();

    public static ApiErrorResponse Error(string code, string message, string? correlationId = null,
                                        Dictionary<string, object?>? extensions = null,
                                        Dictionary<string, string[]>? fieldErrors = null) =>
        new()
        {
            Status = "E",
            Data = new ApiErrorData
            {
                Code = code,
                Message = message,
                CorrelationId = correlationId,
                FieldErrors = fieldErrors,
                Extensions = extensions ?? []
            }
        };
}
