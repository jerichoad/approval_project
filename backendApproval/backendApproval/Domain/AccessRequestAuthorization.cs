using backendApproval.Domain.Entities;

namespace backendApproval.Domain;

public static class AccessRequestAuthorization
{
    public static bool CanView(User actor, AccessRequest r) =>
        actor.IsAuditor
        || r.RequesterId == actor.Id
        || r.ManagerApproverId == actor.Id
        || r.SystemOwnerApproverId == actor.Id;

    public static Guid? AssignedApprover(AccessRequest r) => r.Status switch
    {
        AccessRequestStatus.PendingManagerApproval => r.ManagerApproverId,
        AccessRequestStatus.PendingSystemOwnerApproval => r.SystemOwnerApproverId,
        _ => null
    };

    public static bool CanDecide(User actor, AccessRequest r) =>
        !r.IsTerminal
        && r.RequesterId != actor.Id
        && AssignedApprover(r) == actor.Id;

    public static IReadOnlyList<string> AllowedActions(User actor, AccessRequest r) =>
        CanDecide(actor, r) ? ["approve", "reject"] : [];
}
