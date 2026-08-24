using System;
using System.Collections.Generic;
using System.Text;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Students;

public sealed class Guardian : Entity<Guid>, ITenantOwnedEntity
{
    public const int MaxFullNameLength = 200;
    public const int MaxPhoneNumberLength = 20;

    public Guardian(Guid id, Guid tenantId, string fullName, string phoneNumber)
        : base(ValidateId(id))
    {
        TenantId = ValidateTenantId(tenantId);
        FullName = ValidateFullName(fullName);
        PhoneNumber = ValidatePhoneNumber(phoneNumber);
    }

    public Guid TenantId { get; private set; }

    public string FullName { get; private set; }

    public string PhoneNumber { get; private set; }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Guardian identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Guardian must belong to a tenant.", nameof(tenantId));
        }

        return tenantId;
    }

    private static string ValidateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Guardian full name is required.", nameof(fullName));
        }

        var normalizedName = fullName.Trim();

        if (normalizedName.Length > MaxFullNameLength)
        {
            throw new ArgumentException(
                $"Guardian full name cannot exceed {MaxFullNameLength} characters.",
                nameof(fullName));
        }

        return normalizedName;
    }

    private static string ValidatePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Guardian phone number is required.", nameof(phoneNumber));
        }

        var normalizedPhoneNumber = phoneNumber.Trim();

        if (normalizedPhoneNumber.Length > MaxPhoneNumberLength)
        {
            throw new ArgumentException(
                $"Guardian phone number cannot exceed {MaxPhoneNumberLength} characters.",
                nameof(phoneNumber));
        }

        return normalizedPhoneNumber;
    }
}
