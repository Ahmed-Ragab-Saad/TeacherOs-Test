using System;
using TeacherOS.Domain.Students;
using Xunit;

namespace TeacherOS.Domain.Tests;

public sealed class StudentModuleTests
{
    [Fact]
    public void Student_normalizes_values_and_supports_its_lifecycle_operations()
    {
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var gradeLevelId = Guid.NewGuid();
        var student = new Student(Guid.NewGuid(), tenantId, branchId, gradeLevelId, " ST-001 ", " Mona Ali ", " 12345 ", new DateOnly(2026, 8, 1), " 0100 ", " https://example.test/mona ");

        Assert.Equal("ST-001", student.StudentCode);
        Assert.Equal("Mona Ali", student.FullName);
        Assert.Equal("12345", student.NationalId);
        Assert.Equal(StudentStatus.Active, student.Status);

        student.SuspendAdministratively();
        Assert.Equal(StudentStatus.SuspendedAdministrative, student.Status);
        student.SuspendForNonPayment();
        Assert.Equal(StudentStatus.SuspendedNonPayment, student.Status);
        student.Reactivate();
        Assert.Equal(StudentStatus.Active, student.Status);
        student.Graduate();
        Assert.Equal(StudentStatus.Graduated, student.Status);
    }

    [Fact]
    public void Student_updates_details_and_assignments()
    {
        var student = CreateStudent();
        var branchId = Guid.NewGuid();
        var gradeLevelId = Guid.NewGuid();

        student.UpdateDetails(branchId, gradeLevelId, " Updated name ", " 67890 ", new DateOnly(2026, 9, 1), " ", " ");

        Assert.Equal(branchId, student.BranchId);
        Assert.Equal(gradeLevelId, student.GradeLevelId);
        Assert.Equal("Updated name", student.FullName);
        Assert.Equal("67890", student.NationalId);
        Assert.Null(student.PhoneNumber);
        Assert.Null(student.PhotoUrl);
    }

    [Theory]
    [InlineData("studentId")]
    [InlineData("tenantId")]
    [InlineData("branchId")]
    [InlineData("gradeLevelId")]
    public void Student_rejects_empty_required_identifiers(string field)
    {
        var studentId = field == "studentId" ? Guid.Empty : Guid.NewGuid();
        var tenantId = field == "tenantId" ? Guid.Empty : Guid.NewGuid();
        var branchId = field == "branchId" ? Guid.Empty : Guid.NewGuid();
        var gradeLevelId = field == "gradeLevelId" ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new Student(studentId, tenantId, branchId, gradeLevelId, "ST-001", "Mona Ali", "12345", new DateOnly(2026, 8, 1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Student_rejects_missing_required_text(string value)
    {
        Assert.Throws<ArgumentException>(() => new Student(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), value, "Mona Ali", "12345", new DateOnly(2026, 8, 1)));
        Assert.Throws<ArgumentException>(() => new Student(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ST-001", value, "12345", new DateOnly(2026, 8, 1)));
        Assert.Throws<ArgumentException>(() => new Student(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ST-001", "Mona Ali", value, new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void Branch_grade_level_and_guardian_normalize_and_validate_updates()
    {
        var tenantId = Guid.NewGuid();
        var branch = new Branch(Guid.NewGuid(), tenantId, " Main ");
        var gradeLevel = new GradeLevel(Guid.NewGuid(), tenantId, " Grade 1 ", 1);
        var guardian = new Guardian(Guid.NewGuid(), tenantId, " Parent ", " 0100 ");

        branch.Rename(" Second ");
        gradeLevel.Update(" Grade 2 ", 2);
        guardian.Update(" New Parent ", " 0200 ");

        Assert.Equal("Second", branch.Name);
        Assert.Equal("Grade 2", gradeLevel.Name);
        Assert.Equal(2, gradeLevel.SortOrder);
        Assert.Equal("New Parent", guardian.FullName);
        Assert.Equal("0200", guardian.PhoneNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GradeLevel(Guid.NewGuid(), tenantId, "Grade", -1));
    }

    [Fact]
    public void Student_guardian_link_updates_and_rejects_undefined_relationships()
    {
        var link = new StudentGuardian(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), GuardianRelationshipType.Father, true);

        link.Update(GuardianRelationshipType.Mother, false);

        Assert.Equal(GuardianRelationshipType.Mother, link.RelationshipType);
        Assert.False(link.IsPrimaryContact);
        Assert.Throws<ArgumentOutOfRangeException>(() => link.Update((GuardianRelationshipType)999, true));
    }

    private static Student CreateStudent() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ST-001", "Mona Ali", "12345", new DateOnly(2026, 8, 1));
}
