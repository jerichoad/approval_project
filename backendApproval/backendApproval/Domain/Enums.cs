namespace backendApproval.Domain;

public enum AccessEnvironment { NonProduction, Production }

public enum AccessLevel { Read, Admin }

public enum AccessRequestStatus
{
    PendingManagerApproval,
    PendingSystemOwnerApproval,
    Approved,
    Rejected
}

public enum AuditEventType
{
    RequestCreated,
    ManagerApproved,
    ManagerRejected,
    SystemOwnerApproved,
    SystemOwnerRejected
}

public enum Decision { Approve, Reject }
