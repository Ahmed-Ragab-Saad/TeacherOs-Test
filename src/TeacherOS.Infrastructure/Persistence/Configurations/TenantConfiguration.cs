using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Tenants_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Tenants_Name_NotBlank",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Tenants_Status_Valid",
                "[Status] IN (1, 2, 3, 4, 5)");
        });

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Id)
            .ValueGeneratedNever();

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(Tenant.MaxNameLength)
            .IsRequired();

        builder.Property(tenant => tenant.Status)
            .HasConversion<int>()
            .IsRequired();
    }
}
