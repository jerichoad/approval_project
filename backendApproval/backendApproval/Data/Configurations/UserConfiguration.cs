using backendApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backendApproval.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_email_lowercase", "email = lower(email)");
            t.HasCheckConstraint("ck_users_not_own_manager", "manager_id IS NULL OR manager_id <> id");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.IsAuditor).HasDefaultValue(false);

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");

        b.HasOne(x => x.Manager).WithMany()
         .HasForeignKey(x => x.ManagerId)
         .HasConstraintName("fk_users_manager")
         .OnDelete(DeleteBehavior.Restrict);

        b.HasData(
            new User { Id = SeedData.BobId, Email = "bob@example.local", DisplayName = "Bob", ManagerId = null, IsAuditor = false },
            new User { Id = SeedData.AliceId, Email = "alice@example.local", DisplayName = "Alice", ManagerId = SeedData.BobId, IsAuditor = false },
            new User { Id = SeedData.CarolId, Email = "carol@example.local", DisplayName = "Carol", ManagerId = null, IsAuditor = false },
            new User { Id = SeedData.DanaId, Email = "dana@example.local", DisplayName = "Dana", ManagerId = null, IsAuditor = false },
            new User { Id = SeedData.ErinId, Email = "erin@example.local", DisplayName = "Erin", ManagerId = null, IsAuditor = true });
    }
}
