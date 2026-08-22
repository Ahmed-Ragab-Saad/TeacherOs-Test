using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Identity;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("TenantMemberships", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_TenantMemberships_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_TenantMemberships_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_TenantMemberships_UserId_NotEmpty",
                "[UserId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_TenantMemberships_Status_Valid",
                "[Status] IN (1, 2)");
        });

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .ValueGeneratedNever();

        builder.Property(membership => membership.TenantId)
            .IsRequired();

        builder.Property(membership => membership.UserId)
            .IsRequired();

        builder.Property(membership => membership.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(membership => new { membership.TenantId, membership.UserId })
            .IsUnique()
            .HasDatabaseName("UX_TenantMemberships_TenantId_UserId");

        builder.Property(membership => membership.RoleId);

        builder.HasIndex(membership => membership.UserId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(membership => membership.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
