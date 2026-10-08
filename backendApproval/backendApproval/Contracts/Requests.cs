using System.ComponentModel.DataAnnotations;
using backendApproval.Domain;

namespace backendApproval.Contracts;

public sealed class CreateAccessRequestRequest
{
    [Required, StringLength(64, MinimumLength = 1)]
    public string? ClientRequestId { get; init; }

    [Required] public Guid? ApplicationId { get; init; }

    [Required] public AccessEnvironment? Environment { get; init; }
    [Required] public AccessLevel? AccessLevel { get; init; }

    [Required, StringLength(1000)]
    public string? Justification { get; init; }
}

public sealed class ApproveRequest
{
    [Required] public int? ExpectedVersion { get; init; }
}

public sealed class RejectRequest
{
    [Required] public int? ExpectedVersion { get; init; }
    [Required, StringLength(1000)] public string? Reason { get; init; }
}
