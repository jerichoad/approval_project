using backendApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backendApproval.Data.Configurations;

public sealed class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> b)
    {
        b.ToTable("access_requests", t =>
        {
            t.HasCheckConstraint("ck_access_requests_environment",
                "environment IN ('NonProduction', 'Production')");
            t.HasCheckConstraint("ck_access_requests_access_level",
                "access_level IN ('Read', 'Admin')");
            t.HasCheckConstraint("ck_access_requests_status",
                "status IN ('PendingManagerApproval', 'PendingSystemOwnerApproval', 'Approved', 'Rejected')");

            t.HasCheckConstraint("ck_access_requests_client_request_id_not_blank",
                "length(btrim(client_request_id)) > 0");
            t.HasCheckConstraint("ck_access_requests_justification_not_blank",
                "length(btrim(justification)) > 0");

            t.HasCheckConstraint("ck_access_requests_version_positive", "version >= 1");

            t.HasCheckConstraint("ck_access_requests_manager_not_requester",
                "manager_approver_id <> requester_id");
            t.HasCheckConstraint("ck_access_requests_owner_not_requester",
                "system_owner_approver_id IS NULL OR system_owner_approver_id <> requester_id");

            t.HasCheckConstraint("ck_access_requests_rejection_reason",
                "(status = 'Rejected' AND rejection_reason IS NOT NULL AND length(btrim(rejection_reason)) > 0) " +
                "OR (status <> 'Rejected' AND rejection_reason IS NULL)");

            t.HasCheckConstraint("ck_access_requests_decided_at",
                "(status IN ('Approved', 'Rejected')) = (decided_at IS NOT NULL)");

            t.HasCheckConstraint("ck_access_requests_v1_high_risk_definition",
                "policy_version <> 'v1' OR is_high_risk = (environment = 'Production' OR access_level = 'Admin')");
            t.HasCheckConstraint("ck_access_requests_v1_owner_assignment",
                "policy_version <> 'v1' OR (is_high_risk = (system_owner_approver_id IS NOT NULL))");
            t.HasCheckConstraint("ck_access_requests_v1_low_risk_no_owner_stage",
                "policy_version <> 'v1' OR NOT (status = 'PendingSystemOwnerApproval' AND NOT is_high_risk)");
        });

        b.HasKey(x => x.Id);

        b.Property(x => x.ClientRequestId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Justification).HasMaxLength(1000).IsRequired();
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.Property(x => x.PolicyVersion).HasMaxLength(10).IsRequired();

        b.Property(x => x.Environment).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AccessLevel).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);

        b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();

        b.Ignore(x => x.IsTerminal);

        b.HasIndex(x => new { x.RequesterId, x.ClientRequestId })
         .IsUnique()
         .HasDatabaseName("ux_access_requests_requester_client_request_id");
        b.HasIndex(x => new { x.RequesterId, x.CreatedAt })
         .IsDescending(false, true)
         .HasDatabaseName("ix_access_requests_requester_created");
        b.HasIndex(x => new { x.ManagerApproverId, x.Status })
         .HasDatabaseName("ix_access_requests_manager_status");
        b.HasIndex(x => new { x.SystemOwnerApproverId, x.Status })
         .HasDatabaseName("ix_access_requests_system_owner_status");

        b.HasOne(x => x.Requester).WithMany()
         .HasForeignKey(x => x.RequesterId)
         .HasConstraintName("fk_access_requests_requester")
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Application).WithMany()
         .HasForeignKey(x => x.ApplicationId)
         .HasConstraintName("fk_access_requests_application")
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
         .HasForeignKey(x => x.ManagerApproverId)
         .HasConstraintName("fk_access_requests_manager")
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany()
         .HasForeignKey(x => x.SystemOwnerApproverId)
         .HasConstraintName("fk_access_requests_system_owner")
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PolicyVersion>().WithMany()
         .HasForeignKey(x => x.PolicyVersion)
         .HasConstraintName("fk_access_requests_policy_version")
         .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.AuditEvents).WithOne()
         .HasForeignKey(e => e.AccessRequestId)
         .HasConstraintName("fk_audit_events_access_request")
         .OnDelete(DeleteBehavior.Restrict);
    }
}
