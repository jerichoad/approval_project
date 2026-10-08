using backendApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backendApproval.Data.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events", t =>
        {
            t.HasCheckConstraint("ck_audit_events_event_type",
                "event_type IN ('RequestCreated', 'ManagerApproved', 'ManagerRejected', " +
                "'SystemOwnerApproved', 'SystemOwnerRejected')");
            t.HasCheckConstraint("ck_audit_events_reject_has_reason",
                "event_type NOT IN ('ManagerRejected', 'SystemOwnerRejected') " +
                "OR (reason IS NOT NULL AND length(btrim(reason)) > 0)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();

        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.PolicyVersion).HasMaxLength(10).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(64);

        b.HasIndex(x => new { x.AccessRequestId, x.RequestVersion })
         .IsUnique()
         .HasDatabaseName("ux_audit_events_request_version");
        b.HasIndex(x => new { x.AccessRequestId, x.OccurredAt })
         .HasDatabaseName("ix_audit_events_request_occurred");

        b.HasOne(x => x.Actor).WithMany()
         .HasForeignKey(x => x.ActorId)
         .HasConstraintName("fk_audit_events_actor")
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PolicyVersion>().WithMany()
         .HasForeignKey(x => x.PolicyVersion)
         .HasConstraintName("fk_audit_events_policy_version")
         .OnDelete(DeleteBehavior.Restrict);
    }
}
