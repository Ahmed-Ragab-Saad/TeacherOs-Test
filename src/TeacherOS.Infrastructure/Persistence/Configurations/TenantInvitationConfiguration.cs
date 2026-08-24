using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Identity;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("TenantInvitations", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_CreatedByUserId_NotEmpty",
                "[CreatedByUserId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_Email_NotBlank",
                "LEN(LTRIM(RTRIM([Email]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_NormalizedEmail_NotBlank",
                "LEN(LTRIM(RTRIM([NormalizedEmail]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_TokenHash_NotBlank",
                "LEN(LTRIM(RTRIM([TokenHash]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_TenantInvitations_Expires_After_Created",
                "[ExpiresAtUtc] > [CreatedAtUtc]");
        });

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Id)
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.TenantId)
            .IsRequired();

        builder.Property(invitation => invitation.Email)
            .HasMaxLength(TenantInvitation.MaxEmailLength)
            .IsRequired();

        builder.Property(invitation => invitation.NormalizedEmail)
            .HasMaxLength(TenantInvitation.MaxEmailLength)
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(TenantInvitation.MaxTokenHashLength)
            .IsRequired();

        builder.Property(invitation => invitation.CreatedByUserId)
            .IsRequired();

        builder.Property(invitation => invitation.CreatedAtUtc)
            .IsRequired();

        builder.Property(invitation => invitation.ExpiresAtUtc)
            .IsRequired();

        builder.Property(invitation => invitation.RoleId);
        builder.Property(invitation => invitation.AcceptedAtUtc);
        builder.Property(invitation => invitation.RevokedAtUtc);

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_TenantInvitations_TokenHash");

        builder.HasIndex(invitation => new { invitation.TenantId, invitation.NormalizedEmail })
            .HasDatabaseName("IX_TenantInvitations_TenantId_NormalizedEmail");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(invitation => invitation.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(invitation => invitation.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
