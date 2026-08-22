using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Roles_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Roles_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Roles_Name_NotBlank",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
        });

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.Property(role => role.TenantId)
            .IsRequired();

        builder.Property(role => role.Name)
            .HasMaxLength(Role.MaxNameLength)
            .IsRequired();

        // EF Core primitive collection -> stored as JSON in a single column.
        builder.PrimitiveCollection(role => role.PermissionCodes)
            .HasColumnName("PermissionCodes")
            .IsRequired();

        builder.HasIndex(role => new { role.TenantId, role.Name })
            .IsUnique()
            .HasDatabaseName("UX_Roles_TenantId_Name");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(role => role.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
