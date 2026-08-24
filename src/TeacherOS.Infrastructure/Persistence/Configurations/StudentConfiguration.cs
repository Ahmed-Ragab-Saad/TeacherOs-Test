using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Students;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Students_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Students_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Students_BranchId_NotEmpty",
                "[BranchId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Students_GradeLevelId_NotEmpty",
                "[GradeLevelId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Students_StudentCode_NotBlank",
                "LEN(LTRIM(RTRIM([StudentCode]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Students_FullName_NotBlank",
                "LEN(LTRIM(RTRIM([FullName]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Students_NationalId_NotBlank",
                "LEN(LTRIM(RTRIM([NationalId]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Students_Status_Valid",
                "[Status] IN (1, 2, 3, 4)");
        });

        builder.HasKey(student => student.Id);

        builder.Property(student => student.Id)
            .ValueGeneratedNever();

        builder.Property(student => student.TenantId)
            .IsRequired();

        builder.Property(student => student.BranchId)
            .IsRequired();

        builder.Property(student => student.GradeLevelId)
            .IsRequired();

        builder.Property(student => student.StudentCode)
            .HasMaxLength(Student.MaxStudentCodeLength)
            .IsRequired();

        builder.Property(student => student.FullName)
            .HasMaxLength(Student.MaxFullNameLength)
            .IsRequired();

        builder.Property(student => student.NationalId)
            .HasMaxLength(Student.MaxNationalIdLength)
            .IsRequired();

        builder.Property(student => student.PhoneNumber)
            .HasMaxLength(Student.MaxPhoneNumberLength);

        builder.Property(student => student.PhotoUrl)
            .HasMaxLength(Student.MaxPhotoUrlLength);

        builder.Property(student => student.EnrollmentDate)
            .IsRequired();

        builder.Property(student => student.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(student => new { student.TenantId, student.NationalId })
            .IsUnique()
            .HasDatabaseName("UX_Students_TenantId_NationalId");

        builder.HasIndex(student => new { student.TenantId, student.StudentCode })
            .IsUnique()
            .HasDatabaseName("UX_Students_TenantId_StudentCode");

        builder.HasIndex(student => new { student.TenantId, student.BranchId });

        builder.HasIndex(student => new { student.TenantId, student.GradeLevelId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(student => student.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(student => student.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<GradeLevel>()
            .WithMany()
            .HasForeignKey(student => student.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
