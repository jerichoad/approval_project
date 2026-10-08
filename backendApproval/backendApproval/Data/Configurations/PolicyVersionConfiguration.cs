using backendApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backendApproval.Data.Configurations;

public sealed class PolicyVersionConfiguration : IEntityTypeConfiguration<PolicyVersion>
{
    public void Configure(EntityTypeBuilder<PolicyVersion> b)
    {
        b.ToTable("policy_versions");

        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(10);
        b.Property(x => x.Description).HasColumnType("text").IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(false);

        b.HasIndex(x => x.IsActive).IsUnique()
         .HasFilter("is_active")
         .HasDatabaseName("ux_policy_versions_single_active");

        b.HasData(new PolicyVersion
        {
            Code = SeedData.PolicyV1,
            Description = "High-risk = Environment Production OR AccessLevel Admin. Semua request butuh Manager approval; high-risk lanjut ke System Owner approval.",
            EffectiveFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsActive = true
        });
    }
}
