namespace backendApproval.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public bool IsAuditor { get; set; }
}
