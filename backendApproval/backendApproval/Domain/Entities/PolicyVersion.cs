namespace backendApproval.Domain.Entities;

public sealed class PolicyVersion
{
    public string Code { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTimeOffset EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
}
