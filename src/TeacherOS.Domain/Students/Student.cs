using System;
using System.Collections.Generic;
using System.Text;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Students;

public sealed class Student : Entity<Guid>, ITenantOwnedEntity
{
    public const int MaxFullNameLength = 200;
    public const int MaxNationalIdLength = 20;
    public const int MaxStudentCodeLength = 30;
    public const int MaxPhoneNumberLength = 20;
    public const int MaxPhotoUrlLength = 2048;

    public Student(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid gradeLevelId,
        string studentCode,
        string fullName,
        string nationalId,
        DateOnly enrollmentDate,
        string? phoneNumber = null,
        string? photoUrl = null)
        : base(ValidateId(id))
    {
        TenantId = ValidateTenantId(tenantId);
        BranchId = ValidateRequiredId(branchId, nameof(branchId), "Branch");
        GradeLevelId = ValidateRequiredId(gradeLevelId, nameof(gradeLevelId), "Grade level");
        StudentCode = ValidateStudentCode(studentCode);
        FullName = ValidateFullName(fullName);
        NationalId = ValidateNationalId(nationalId);
        EnrollmentDate = enrollmentDate;
        PhoneNumber = NormalizeOptional(phoneNumber, MaxPhoneNumberLength, nameof(phoneNumber));
        PhotoUrl = NormalizeOptional(photoUrl, MaxPhotoUrlLength, nameof(photoUrl));
        Status = StudentStatus.Active;
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid GradeLevelId { get; private set; }

    public string StudentCode { get; private set; }

    public string FullName { get; private set; }

    public string NationalId { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? PhotoUrl { get; private set; }

    public DateOnly EnrollmentDate { get; private set; }

    public StudentStatus Status { get; private set; }

    public void SuspendAdministratively() => Status = StudentStatus.SuspendedAdministrative;

    public void SuspendForNonPayment() => Status = StudentStatus.SuspendedNonPayment;

    public void Reactivate() => Status = StudentStatus.Active;

    public void Graduate() => Status = StudentStatus.Graduated;

    public void AssignToGradeLevel(Guid gradeLevelId)
    {
        GradeLevelId = ValidateRequiredId(gradeLevelId, nameof(gradeLevelId), "Grade level");
    }

    public void TransferToBranch(Guid branchId)
    {
        BranchId = ValidateRequiredId(branchId, nameof(branchId), "Branch");
    }

    public void UpdateDetails(
        Guid branchId,
        Guid gradeLevelId,
        string fullName,
        string nationalId,
        DateOnly enrollmentDate,
        string? phoneNumber,
        string? photoUrl)
    {
        BranchId = ValidateRequiredId(branchId, nameof(branchId), "Branch");
        GradeLevelId = ValidateRequiredId(gradeLevelId, nameof(gradeLevelId), "Grade level");
        FullName = ValidateFullName(fullName);
        NationalId = ValidateNationalId(nationalId);
        EnrollmentDate = enrollmentDate;
        PhoneNumber = NormalizeOptional(phoneNumber, MaxPhoneNumberLength, nameof(phoneNumber));
        PhotoUrl = NormalizeOptional(photoUrl, MaxPhotoUrlLength, nameof(photoUrl));
    }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Student identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Student must belong to a tenant.", nameof(tenantId));
        }

        return tenantId;
    }

    private static Guid ValidateRequiredId(Guid id, string parameterName, string subject)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"{subject} is required.", parameterName);
        }

        return id;
    }

    private static string ValidateStudentCode(string studentCode)
    {
        if (string.IsNullOrWhiteSpace(studentCode))
        {
            throw new ArgumentException("Student code is required.", nameof(studentCode));
        }

        var normalizedCode = studentCode.Trim();

        if (normalizedCode.Length > MaxStudentCodeLength)
        {
            throw new ArgumentException(
                $"Student code cannot exceed {MaxStudentCodeLength} characters.",
                nameof(studentCode));
        }

        return normalizedCode;
    }

    private static string ValidateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Student full name is required.", nameof(fullName));
        }

        var normalizedName = fullName.Trim();

        if (normalizedName.Length > MaxFullNameLength)
        {
            throw new ArgumentException(
                $"Student full name cannot exceed {MaxFullNameLength} characters.",
                nameof(fullName));
        }

        return normalizedName;
    }

    private static string ValidateNationalId(string nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
        {
            throw new ArgumentException("Student national ID is required.", nameof(nationalId));
        }

        var normalizedNationalId = nationalId.Trim();

        if (normalizedNationalId.Length > MaxNationalIdLength)
        {
            throw new ArgumentException(
                $"Student national ID cannot exceed {MaxNationalIdLength} characters.",
                nameof(nationalId));
        }

        return normalizedNationalId;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException(
                $"{parameterName} cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }
}
