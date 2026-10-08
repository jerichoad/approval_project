using backendApproval.Domain;
using backendApproval.Domain.Entities;

namespace backendApproval.Policies;

public interface IApprovalPolicy
{
    string Version { get; }
    bool IsHighRisk(AccessEnvironment environment, AccessLevel accessLevel);
    AccessRequestStatus NextStatusOnApprove(AccessRequest request);
}
