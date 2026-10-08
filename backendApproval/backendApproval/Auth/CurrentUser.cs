using backendApproval.Domain.Entities;

namespace backendApproval.Auth;

public sealed class CurrentUser
{
    private User? _user;
    public User User => _user ?? throw new InvalidOperationException("No current user.");
    public bool IsAuthenticated => _user is not null;
    public void Set(User user) => _user = user;
}
