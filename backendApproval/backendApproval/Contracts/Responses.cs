using backendApproval.Domain;

namespace backendApproval.Contracts;

public sealed record UserSummaryResponse(Guid Id, string Email, string DisplayName);

public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsAuditor,
    bool HasDirectReports,
    IReadOnlyList<ApplicationResponse> OwnedApplications);

public sealed record ApplicationResponse(
    Guid Id,
    string Code,
    string Name,
    UserSummaryResponse SystemOwner);

public sealed record AuditEventResponse(
    AuditEventType EventType,
    string Actor,
    AccessRequestStatus? FromStatus,
    AccessRequestStatus ToStatus,
    string? Reason,
    int RequestVersion,
    DateTimeOffset OccurredAt);

public sealed record AccessRequestSummaryResponse(
    Guid Id,
    string ClientRequestId,
    UserSummaryResponse Requester,
    ApplicationResponse Application,
    AccessEnvironment Environment,
    AccessLevel AccessLevel,
    AccessRequestStatus Status,
    bool IsHighRisk,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DecidedAt);

public sealed record AccessRequestDetailResponse(
    Guid Id,
    string ClientRequestId,
    UserSummaryResponse Requester,
    ApplicationResponse Application,
    AccessEnvironment Environment,
    AccessLevel AccessLevel,
    string Justification,
    AccessRequestStatus Status,
    string PolicyVersion,
    bool IsHighRisk,
    UserSummaryResponse? CurrentApprover,
    string? RejectionReason,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DecidedAt,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<AuditEventResponse> AuditTrail);
