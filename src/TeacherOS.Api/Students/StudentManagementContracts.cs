using TeacherOS.Domain.Students;

namespace TeacherOS.Api.Students;

internal sealed record BranchWriteRequest(string Name);
internal sealed record BranchResponse(Guid Id, string Name);

internal sealed record GradeLevelWriteRequest(string Name, int SortOrder);
internal sealed record GradeLevelResponse(Guid Id, string Name, int SortOrder);

internal sealed record StudentCreateRequest(
    string StudentCode,
    string FullName,
    string NationalId,
    Guid BranchId,
    Guid GradeLevelId,
    DateOnly EnrollmentDate,
    string? PhoneNumber,
    string? PhotoUrl);

internal sealed record StudentUpdateRequest(
    string FullName,
    string NationalId,
    Guid BranchId,
    Guid GradeLevelId,
    DateOnly EnrollmentDate,
    string? PhoneNumber,
    string? PhotoUrl);

internal sealed record StudentBranchAssignmentRequest(Guid BranchId);

internal sealed record StudentGradeLevelAssignmentRequest(Guid GradeLevelId);

internal sealed record StudentResponse(
    Guid Id,
    string StudentCode,
    string FullName,
    string NationalId,
    Guid BranchId,
    Guid GradeLevelId,
    StudentStatus Status,
    DateOnly EnrollmentDate,
    string? PhoneNumber,
    string? PhotoUrl);
