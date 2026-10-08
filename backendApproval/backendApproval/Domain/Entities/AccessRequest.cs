namespace backendApproval.Domain.Entities;

public sealed class AccessRequest
{
    public Guid Id { get; set; }
    public string ClientRequestId { get; set; } = default!;

    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = default!;
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = default!;

    public AccessEnvironment Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public string Justification { get; set; } = default!;

    public AccessRequestStatus Status { get; set; }
    public string PolicyVersion { get; set; } = default!;
    public bool IsHighRisk { get; set; }

    public Guid ManagerApproverId { get; set; }
    public Guid? SystemOwnerApproverId { get; set; }

    public string? RejectionReason { get; set; }
    public int Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    public List<AuditEvent> AuditEvents { get; set; } = new();

    public bool IsTerminal =>
        Status is AccessRequestStatus.Approved or AccessRequestStatus.Rejected;
}
