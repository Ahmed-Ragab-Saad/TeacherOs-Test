using System.Collections.Generic;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Students;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Students;

namespace TeacherOS.Application.Students;

public sealed class StudentManagementHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IStudentManagementStore store,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<IReadOnlyList<Branch>>> ListBranchesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        return accessError is null
            ? Result<IReadOnlyList<Branch>>.Success(await store.ListBranchesAsync(tenantId, cancellationToken))
            : Result<IReadOnlyList<Branch>>.Failure(accessError);
    }

    public async Task<Result<Branch>> GetBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        if (accessError is not null) return Result<Branch>.Failure(accessError);
        var branch = await store.GetBranchAsync(tenantId, branchId, cancellationToken);
        return branch is null ? Result<Branch>.Failure(StudentManagementErrors.BranchNotFound) : Result<Branch>.Success(branch);
    }

    public async Task<Result<Branch>> CreateBranchAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        if (accessError is not null) return Result<Branch>.Failure(accessError);
        if (!IsValidRequiredText(name, Branch.MaxNameLength)) return Result<Branch>.Failure(StudentManagementErrors.InvalidInput);
        if (await store.BranchNameExistsAsync(tenantId, name.Trim(), null, cancellationToken)) return Result<Branch>.Failure(StudentManagementErrors.BranchNameExists);
        var branch = new Branch(Guid.NewGuid(), tenantId, name);
        store.AddBranch(branch);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Branch>.Success(branch);
    }

    public async Task<Result<Branch>> UpdateBranchAsync(Guid tenantId, Guid branchId, string name, CancellationToken cancellationToken = default)
    {
        var branchResult = await GetBranchAsync(tenantId, branchId, cancellationToken);
        if (branchResult.IsFailure) return branchResult;
        if (!IsValidRequiredText(name, Branch.MaxNameLength)) return Result<Branch>.Failure(StudentManagementErrors.InvalidInput);
        if (await store.BranchNameExistsAsync(tenantId, name.Trim(), branchId, cancellationToken)) return Result<Branch>.Failure(StudentManagementErrors.BranchNameExists);
        branchResult.Value.Rename(name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return branchResult;
    }

    public async Task<Result<IReadOnlyList<GradeLevel>>> ListGradeLevelsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        return accessError is null
            ? Result<IReadOnlyList<GradeLevel>>.Success(await store.ListGradeLevelsAsync(tenantId, cancellationToken))
            : Result<IReadOnlyList<GradeLevel>>.Failure(accessError);
    }

    public async Task<Result<GradeLevel>> GetGradeLevelAsync(Guid tenantId, Guid gradeLevelId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        if (accessError is not null) return Result<GradeLevel>.Failure(accessError);
        var gradeLevel = await store.GetGradeLevelAsync(tenantId, gradeLevelId, cancellationToken);
        return gradeLevel is null ? Result<GradeLevel>.Failure(StudentManagementErrors.GradeLevelNotFound) : Result<GradeLevel>.Success(gradeLevel);
    }

    public async Task<Result<GradeLevel>> CreateGradeLevelAsync(Guid tenantId, string name, int sortOrder, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        if (accessError is not null) return Result<GradeLevel>.Failure(accessError);
        if (!IsValidRequiredText(name, GradeLevel.MaxNameLength) || sortOrder < 0) return Result<GradeLevel>.Failure(StudentManagementErrors.InvalidInput);
        if (await store.GradeLevelNameExistsAsync(tenantId, name.Trim(), null, cancellationToken)) return Result<GradeLevel>.Failure(StudentManagementErrors.GradeLevelNameExists);
        var gradeLevel = new GradeLevel(Guid.NewGuid(), tenantId, name, sortOrder);
        store.AddGradeLevel(gradeLevel);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<GradeLevel>.Success(gradeLevel);
    }

    public async Task<Result<GradeLevel>> UpdateGradeLevelAsync(Guid tenantId, Guid gradeLevelId, string name, int sortOrder, CancellationToken cancellationToken = default)
    {
        var gradeLevelResult = await GetGradeLevelAsync(tenantId, gradeLevelId, cancellationToken);
        if (gradeLevelResult.IsFailure) return gradeLevelResult;
        if (!IsValidRequiredText(name, GradeLevel.MaxNameLength) || sortOrder < 0) return Result<GradeLevel>.Failure(StudentManagementErrors.InvalidInput);
        if (await store.GradeLevelNameExistsAsync(tenantId, name.Trim(), gradeLevelId, cancellationToken)) return Result<GradeLevel>.Failure(StudentManagementErrors.GradeLevelNameExists);
        gradeLevelResult.Value.Update(name, sortOrder);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gradeLevelResult;
    }

    public async Task<Result<Student>> GetStudentAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        if (accessError is not null) return Result<Student>.Failure(accessError);
        var student = await store.GetStudentAsync(tenantId, studentId, cancellationToken);
        return student is null ? Result<Student>.Failure(StudentManagementErrors.StudentNotFound) : Result<Student>.Success(student);
    }

    public async Task<Result<Student>> CreateStudentAsync(Guid tenantId, string studentCode, string fullName, string nationalId, Guid branchId, Guid gradeLevelId, DateOnly enrollmentDate, string? phoneNumber, string? photoUrl, CancellationToken cancellationToken = default)
    {
        var accessError = ValidateAccess(tenantId);
        if (accessError is not null) return Result<Student>.Failure(accessError);
        if (!IsValidStudentInput(studentCode, fullName, nationalId, branchId, gradeLevelId, phoneNumber, photoUrl)) return Result<Student>.Failure(StudentManagementErrors.InvalidInput);
        if (await store.GetBranchAsync(tenantId, branchId, cancellationToken) is null) return Result<Student>.Failure(StudentManagementErrors.BranchNotFound);
        if (await store.GetGradeLevelAsync(tenantId, gradeLevelId, cancellationToken) is null) return Result<Student>.Failure(StudentManagementErrors.GradeLevelNotFound);
        if (await store.StudentCodeExistsAsync(tenantId, studentCode.Trim(), null, cancellationToken)) return Result<Student>.Failure(StudentManagementErrors.StudentCodeExists);
        if (await store.NationalIdExistsAsync(tenantId, nationalId.Trim(), null, cancellationToken)) return Result<Student>.Failure(StudentManagementErrors.NationalIdExists);
        var student = new Student(Guid.NewGuid(), tenantId, branchId, gradeLevelId, studentCode, fullName, nationalId, enrollmentDate, phoneNumber, photoUrl);
        store.AddStudent(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Student>.Success(student);
    }

    public async Task<Result<Student>> UpdateStudentAsync(Guid tenantId, Guid studentId, string fullName, string nationalId, Guid branchId, Guid gradeLevelId, DateOnly enrollmentDate, string? phoneNumber, string? photoUrl, CancellationToken cancellationToken = default)
    {
        var studentResult = await GetStudentAsync(tenantId, studentId, cancellationToken);
        if (studentResult.IsFailure) return studentResult;
        if (!IsValidStudentInput(null, fullName, nationalId, branchId, gradeLevelId, phoneNumber, photoUrl)) return Result<Student>.Failure(StudentManagementErrors.InvalidInput);
        if (await store.GetBranchAsync(tenantId, branchId, cancellationToken) is null) return Result<Student>.Failure(StudentManagementErrors.BranchNotFound);
        if (await store.GetGradeLevelAsync(tenantId, gradeLevelId, cancellationToken) is null) return Result<Student>.Failure(StudentManagementErrors.GradeLevelNotFound);
        if (await store.NationalIdExistsAsync(tenantId, nationalId.Trim(), studentId, cancellationToken)) return Result<Student>.Failure(StudentManagementErrors.NationalIdExists);
        studentResult.Value.UpdateDetails(branchId, gradeLevelId, fullName, nationalId, enrollmentDate, phoneNumber, photoUrl);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return studentResult;
    }

    private Error? ValidateAccess(Guid tenantId)
    {
        if (!currentUser.IsAuthenticated) return new Error("Authentication.Unauthorized", "Authentication is required.", ErrorType.Unauthorized);
        return !tenantContext.IsAvailable || tenantContext.TenantId != tenantId
            ? new Error("Tenancy.AccessDenied", "Access to the selected tenant is denied.", ErrorType.Forbidden)
            : null;
    }

    private static bool IsValidStudentInput(string? studentCode, string? fullName, string? nationalId, Guid branchId, Guid gradeLevelId, string? phoneNumber, string? photoUrl)
    {
        return (studentCode is null || IsValidRequiredText(studentCode, Student.MaxStudentCodeLength)) &&
               IsValidRequiredText(fullName, Student.MaxFullNameLength) &&
               IsValidRequiredText(nationalId, Student.MaxNationalIdLength) &&
               branchId != Guid.Empty &&
               gradeLevelId != Guid.Empty &&
               IsValidOptionalText(phoneNumber, Student.MaxPhoneNumberLength) &&
               IsValidOptionalText(photoUrl, Student.MaxPhotoUrlLength);
    }

    private static bool IsValidRequiredText(string? value, int maximumLength)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;
    }

    private static bool IsValidOptionalText(string? value, int maximumLength)
    {
        return string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maximumLength;
    }
}
