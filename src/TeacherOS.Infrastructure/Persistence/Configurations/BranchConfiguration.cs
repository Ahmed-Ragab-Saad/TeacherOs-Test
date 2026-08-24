using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Students;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Branches_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Branches_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Branches_Name_NotBlank",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
        });

        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Id)
            .ValueGeneratedNever();

        builder.Property(branch => branch.TenantId)
            .IsRequired();

        builder.Property(branch => branch.Name)
            .HasMaxLength(Branch.MaxNameLength)
            .IsRequired();

        builder.HasIndex(branch => new { branch.TenantId, branch.Name })
            .IsUnique()
            .HasDatabaseName("UX_Branches_TenantId_Name");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(branch => branch.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
