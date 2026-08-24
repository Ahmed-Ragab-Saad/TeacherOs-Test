using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Students;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("StudentGuardians", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_StudentGuardians_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_StudentGuardians_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_StudentGuardians_StudentId_NotEmpty",
                "[StudentId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_StudentGuardians_GuardianId_NotEmpty",
                "[GuardianId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_StudentGuardians_RelationshipType_Valid",
                "[RelationshipType] IN (1, 2, 3, 4)");
        });

        builder.HasKey(studentGuardian => studentGuardian.Id);

        builder.Property(studentGuardian => studentGuardian.Id)
            .ValueGeneratedNever();

        builder.Property(studentGuardian => studentGuardian.TenantId)
            .IsRequired();

        builder.Property(studentGuardian => studentGuardian.RelationshipType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(studentGuardian => studentGuardian.IsPrimaryContact)
            .IsRequired();

        builder.HasIndex(studentGuardian => new { studentGuardian.StudentId, studentGuardian.GuardianId })
            .IsUnique()
            .HasDatabaseName("UX_StudentGuardians_StudentId_GuardianId");

        builder.HasIndex(studentGuardian => studentGuardian.GuardianId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(studentGuardian => studentGuardian.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(studentGuardian => studentGuardian.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Guardian>()
            .WithMany()
            .HasForeignKey(studentGuardian => studentGuardian.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
