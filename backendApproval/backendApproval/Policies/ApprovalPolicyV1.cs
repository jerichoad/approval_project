using backendApproval.Domain;
using backendApproval.Domain.Entities;

namespace backendApproval.Policies;

public sealed class ApprovalPolicyV1 : IApprovalPolicy
{
    public string Version => "v1";

    public bool IsHighRisk(AccessEnvironment environment, AccessLevel accessLevel) =>
        environment == AccessEnvironment.Production || accessLevel == AccessLevel.Admin;

    public AccessRequestStatus NextStatusOnApprove(AccessRequest request) => request.Status switch
    {
        AccessRequestStatus.PendingManagerApproval => request.IsHighRisk
            ? AccessRequestStatus.PendingSystemOwnerApproval
            : AccessRequestStatus.Approved,
        AccessRequestStatus.PendingSystemOwnerApproval => AccessRequestStatus.Approved,
        _ => throw new InvalidOperationException($"No approve transition from {request.Status}.")
    };
}
