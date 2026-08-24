using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Students;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class GradeLevelConfiguration : IEntityTypeConfiguration<GradeLevel>
{
    public void Configure(EntityTypeBuilder<GradeLevel> builder)
    {
        builder.ToTable("GradeLevels", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_GradeLevels_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_GradeLevels_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_GradeLevels_Name_NotBlank",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_GradeLevels_SortOrder_NonNegative",
                "[SortOrder] >= 0");
        });

        builder.HasKey(gradeLevel => gradeLevel.Id);

        builder.Property(gradeLevel => gradeLevel.Id)
            .ValueGeneratedNever();

        builder.Property(gradeLevel => gradeLevel.TenantId)
            .IsRequired();

        builder.Property(gradeLevel => gradeLevel.Name)
            .HasMaxLength(GradeLevel.MaxNameLength)
            .IsRequired();

        builder.Property(gradeLevel => gradeLevel.SortOrder)
            .IsRequired();

        builder.HasIndex(gradeLevel => new { gradeLevel.TenantId, gradeLevel.Name })
            .IsUnique()
            .HasDatabaseName("UX_GradeLevels_TenantId_Name");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(gradeLevel => gradeLevel.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
