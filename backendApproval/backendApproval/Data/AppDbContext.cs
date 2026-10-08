using backendApproval.Domain;
using backendApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backendApproval.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<AccessEnvironment>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<AccessLevel>().HaveConversion<string>().HaveMaxLength(10);
        b.Properties<AccessRequestStatus>().HaveConversion<string>().HaveMaxLength(40);
        b.Properties<AuditEventType>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
