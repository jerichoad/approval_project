namespace backendApproval.Domain.Entities;

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid AccessRequestId { get; set; }
    public AuditEventType EventType { get; set; }
    public Guid ActorId { get; set; }
    public User Actor { get; set; } = default!;
    public AccessRequestStatus? FromStatus { get; set; }
    public AccessRequestStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public string PolicyVersion { get; set; } = default!;
    public int RequestVersion { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
