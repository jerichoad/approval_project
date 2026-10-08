using backendApproval.Auth;
using backendApproval.Contracts;
using backendApproval.Data;
using backendApproval.Domain;
using backendApproval.Domain.Entities;
using backendApproval.Errors;
using backendApproval.Observability;
using backendApproval.Policies;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Services;

public sealed record CreateResult(AccessRequest Request, bool IsNew)
{
    public static CreateResult Created(AccessRequest r) => new(r, true);
    public static CreateResult Replayed(AccessRequest r) => new(r, false);
}

public sealed class AccessRequestService(
    AppDbContext db,
    CurrentUser currentUser,
    ApprovalPolicyRegistry policies,
    CorrelationContext correlation,
    TimeProvider clock,
    ILogger<AccessRequestService> logger)
{
    public async Task<CreateResult> CreateAsync(CreateAccessRequestRequest input, CancellationToken ct)
    {
        var actor = currentUser.User;
        var clientRequestId = input.ClientRequestId!.Trim();

        var existing = await FindByClientRequestIdAsync(actor.Id, clientRequestId, ct);
        if (existing is not null) return Replay(existing, input);

        var app = await db.Applications.SingleOrDefaultAsync(a => a.Id == input.ApplicationId, ct)
            ?? throw new BusinessRuleException("UNKNOWN_APPLICATION", "Aplikasi tidak dikenal.");
        if (actor.ManagerId is null)
            throw new BusinessRuleException("NO_MANAGER", "Requester tidak memiliki manager untuk approval.");

        var activeCode = await db.PolicyVersions.Where(p => p.IsActive).Select(p => p.Code).SingleAsync(ct);
        var policy = policies.Get(activeCode);
        var isHighRisk = policy.IsHighRisk(input.Environment!.Value, input.AccessLevel!.Value);

        if (isHighRisk && app.SystemOwnerId == actor.Id)
            throw new BusinessRuleException("NO_ELIGIBLE_APPROVER",
                "Requester adalah System Owner aplikasi ini, sehingga tidak ada approver yang sah.");

        var now = clock.GetUtcNow();
        var request = new AccessRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = clientRequestId,
            RequesterId = actor.Id,
            ApplicationId = app.Id,
            Environment = input.Environment!.Value,
            AccessLevel = input.AccessLevel!.Value,
            Justification = input.Justification!.Trim(),
            Status = AccessRequestStatus.PendingManagerApproval,
            PolicyVersion = policy.Version,
            IsHighRisk = isHighRisk,
            ManagerApproverId = actor.ManagerId.Value,
            SystemOwnerApproverId = isHighRisk ? app.SystemOwnerId : null,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        request.AuditEvents.Add(NewAudit(request, AuditEventType.RequestCreated, actor.Id,
            from: null, to: request.Status, reason: null, now));

        db.AccessRequests.Add(request);

        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Access request {RequestId} created (clientRequestId {ClientRequestId})",
                request.Id, clientRequestId);
            return CreateResult.Created(request);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("ux_access_requests_requester_client_request_id"))
        {
            db.ChangeTracker.Clear();
            var winner = await FindByClientRequestIdAsync(actor.Id, clientRequestId, ct)
                ?? throw new InvalidOperationException("Unique violation but no existing row found.", ex);
            logger.LogInformation("Idempotent replay after race for {ClientRequestId}", clientRequestId);
            return Replay(winner, input);
        }
    }

    public async Task<AccessRequest> DecideAsync(Guid id, Decision decision, int expectedVersion,
                                                 string? reason, CancellationToken ct)
    {
        var actor = currentUser.User;
        var request = await db.AccessRequests.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("ACCESS_REQUEST_NOT_FOUND", $"Access request {id} tidak ditemukan.");

        if (!AccessRequestAuthorization.CanView(actor, request))
            throw new ForbiddenException("NOT_ASSIGNED_APPROVER", "Anda bukan approver untuk request ini.");

        if (request.Version != expectedVersion)
            throw Stale(request.Version, request.Status);

        if (request.IsTerminal)
            throw new ConflictException("INVALID_TRANSITION",
                $"Request sudah {request.Status} dan tidak dapat diproses lagi.");

        if (request.RequesterId == actor.Id)
            throw new ForbiddenException("SELF_APPROVAL_NOT_ALLOWED",
                "Requester tidak boleh memproses request miliknya sendiri.");

        if (AccessRequestAuthorization.AssignedApprover(request) != actor.Id)
            throw new ForbiddenException("NOT_ASSIGNED_APPROVER", "Anda bukan approver untuk tahap ini.");

        var policy = policies.Get(request.PolicyVersion);
        var from = request.Status;
        var isManagerStage = from == AccessRequestStatus.PendingManagerApproval;
        var now = clock.GetUtcNow();

        AuditEventType eventType;
        if (decision == Decision.Approve)
        {
            request.Status = policy.NextStatusOnApprove(request);
            eventType = isManagerStage ? AuditEventType.ManagerApproved : AuditEventType.SystemOwnerApproved;
        }
        else
        {
            request.Status = AccessRequestStatus.Rejected;
            request.RejectionReason = reason!.Trim();
            eventType = isManagerStage ? AuditEventType.ManagerRejected : AuditEventType.SystemOwnerRejected;
        }

        if (request.IsTerminal) request.DecidedAt = now;
        request.UpdatedAt = now;

        db.Entry(request).Property(r => r.Version).OriginalValue = expectedVersion;
        request.Version = expectedVersion + 1;

        db.AuditEvents.Add(NewAudit(request, eventType, actor.Id, from, request.Status,
            decision == Decision.Reject ? request.RejectionReason : null, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict on {RequestId} at version {Version}", id, expectedVersion);
            throw await StaleFromDbAsync(id, ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("ux_audit_events_request_version"))
        {
            logger.LogWarning("Duplicate audit version on {RequestId} at version {Version}", id, expectedVersion);
            throw await StaleFromDbAsync(id, ct);
        }

        logger.LogInformation("Access request {RequestId}: {From} -> {To} by {Actor}",
            id, from, request.Status, actor.Email);
        return request;
    }

    public async Task<AccessRequestDetailResponse> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var actor = currentUser.User;
        var request = await db.AccessRequests.AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.AuditEvents).ThenInclude(e => e.Actor)
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("ACCESS_REQUEST_NOT_FOUND", $"Access request {id} tidak ditemukan.");

        if (!AccessRequestAuthorization.CanView(actor, request))
            throw new ForbiddenException("FORBIDDEN", "Anda tidak memiliki akses ke request ini.");

        var approverId = AccessRequestAuthorization.AssignedApprover(request);
        var currentApprover = approverId is null
            ? null
            : await db.Users.AsNoTracking()
                .Where(u => u.Id == approverId)
                .Select(u => new UserSummaryResponse(u.Id, u.Email, u.DisplayName))
                .SingleOrDefaultAsync(ct);

        return new AccessRequestDetailResponse(
            request.Id,
            request.ClientRequestId,
            ToUser(request.Requester),
            ToApplication(request.Application),
            request.Environment,
            request.AccessLevel,
            request.Justification,
            request.Status,
            request.PolicyVersion,
            request.IsHighRisk,
            currentApprover,
            request.RejectionReason,
            request.Version,
            request.CreatedAt,
            request.UpdatedAt,
            request.DecidedAt,
            AccessRequestAuthorization.AllowedActions(actor, request),
            request.AuditEvents
                .OrderBy(e => e.RequestVersion)
                .Select(e => new AuditEventResponse(
                    e.EventType, e.Actor.Email, e.FromStatus, e.ToStatus, e.Reason, e.RequestVersion, e.OccurredAt))
                .ToList());
    }

    public async Task<IReadOnlyList<AccessRequestSummaryResponse>> ListMineAsync(CancellationToken ct)
    {
        var actor = currentUser.User;
        var rows = await SummaryQuery()
            .Where(r => r.RequesterId == actor.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<AccessRequestSummaryResponse>> ListAllAsync(CancellationToken ct)
    {
        var actor = currentUser.User;
        if (!actor.IsAuditor)
            throw new ForbiddenException("FORBIDDEN", "Hanya auditor yang dapat melihat semua request.");

        var rows = await SummaryQuery()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<AccessRequestSummaryResponse>> InboxAsync(CancellationToken ct)
    {
        var me = currentUser.User.Id;
        var rows = await SummaryQuery()
            .Where(r =>
                (r.Status == AccessRequestStatus.PendingManagerApproval && r.ManagerApproverId == me) ||
                (r.Status == AccessRequestStatus.PendingSystemOwnerApproval && r.SystemOwnerApproverId == me))
            .Where(r => r.RequesterId != me)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    private IQueryable<AccessRequest> SummaryQuery() =>
        db.AccessRequests.AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner);

    private Task<AccessRequest?> FindByClientRequestIdAsync(Guid requesterId, string clientRequestId,
                                                            CancellationToken ct) =>
        db.AccessRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.RequesterId == requesterId && r.ClientRequestId == clientRequestId, ct);

    private static CreateResult Replay(AccessRequest existing, CreateAccessRequestRequest input)
    {
        var samePayload =
            existing.ApplicationId == input.ApplicationId &&
            existing.Environment == input.Environment &&
            existing.AccessLevel == input.AccessLevel &&
            existing.Justification == input.Justification!.Trim();

        if (!samePayload)
            throw new ConflictException("IDEMPOTENCY_KEY_REUSED",
                "ClientRequestId sudah dipakai untuk request dengan isi berbeda.",
                new() { ["existingRequestId"] = existing.Id });

        return CreateResult.Replayed(existing);
    }

    private static ConflictException Stale(int currentVersion, AccessRequestStatus currentStatus) =>
        new("STALE_VERSION",
            "Request sudah berubah sejak terakhir dimuat. Muat ulang lalu coba lagi.",
            new()
            {
                ["currentVersion"] = currentVersion,
                ["currentStatus"] = currentStatus.ToString()
            });

    private async Task<ConflictException> StaleFromDbAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var current = await db.AccessRequests.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Version, r.Status })
            .SingleAsync(ct);
        return Stale(current.Version, current.Status);
    }

    private AuditEvent NewAudit(AccessRequest r, AuditEventType type, Guid actorId,
                                AccessRequestStatus? from, AccessRequestStatus to,
                                string? reason, DateTimeOffset now) => new()
    {
        AccessRequestId = r.Id,
        EventType = type,
        ActorId = actorId,
        FromStatus = from,
        ToStatus = to,
        Reason = reason,
        PolicyVersion = r.PolicyVersion,
        RequestVersion = r.Version,
        CorrelationId = correlation.Id,
        OccurredAt = now
    };

    internal static UserSummaryResponse ToUser(User u) => new(u.Id, u.Email, u.DisplayName);

    internal static ApplicationResponse ToApplication(Application a) =>
        new(a.Id, a.Code, a.Name, ToUser(a.SystemOwner));

    private static AccessRequestSummaryResponse ToSummary(AccessRequest r) => new(
        r.Id,
        r.ClientRequestId,
        ToUser(r.Requester),
        ToApplication(r.Application),
        r.Environment,
        r.AccessLevel,
        r.Status,
        r.IsHighRisk,
        r.Version,
        r.CreatedAt,
        r.UpdatedAt,
        r.DecidedAt);
}
