namespace backendApproval.Domain.Entities;

public sealed class Application
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid SystemOwnerId { get; set; }
    public User SystemOwner { get; set; } = default!;
}
