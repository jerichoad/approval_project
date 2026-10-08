namespace backendApproval.Policies;

public sealed class ApprovalPolicyRegistry
{
    private readonly Dictionary<string, IApprovalPolicy> _byVersion;

    public ApprovalPolicyRegistry(IEnumerable<IApprovalPolicy> policies) =>
        _byVersion = policies.ToDictionary(p => p.Version);

    public IApprovalPolicy Get(string version) =>
        _byVersion.TryGetValue(version, out var policy)
            ? policy
            : throw new InvalidOperationException($"Unknown policy version '{version}'.");
}
