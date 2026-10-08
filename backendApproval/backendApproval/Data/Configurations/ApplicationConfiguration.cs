using backendApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backendApproval.Data.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> b)
    {
        b.ToTable("applications");

        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();

        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_applications_code");

        b.HasOne(x => x.SystemOwner).WithMany()
         .HasForeignKey(x => x.SystemOwnerId)
         .HasConstraintName("fk_applications_system_owner")
         .OnDelete(DeleteBehavior.Restrict);

        b.HasData(
            new Application { Id = SeedData.CrmId, Code = "CRM", Name = "CRM", SystemOwnerId = SeedData.CarolId },
            new Application { Id = SeedData.FinancePortalId, Code = "FINANCE_PORTAL", Name = "Finance Portal", SystemOwnerId = SeedData.DanaId });
    }
}
