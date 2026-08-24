using TeacherOS.Domain.Students;

namespace TeacherOS.Application.Students;

public sealed record StudentListItem(
    Guid Id,
    string StudentCode,
    string FullName,
    string NationalId,
    Guid BranchId,
    string BranchName,
    Guid GradeLevelId,
    string GradeLevelName,
    StudentStatus Status,
    DateOnly EnrollmentDate,
    string? PhoneNumber,
    string? PhotoUrl);
