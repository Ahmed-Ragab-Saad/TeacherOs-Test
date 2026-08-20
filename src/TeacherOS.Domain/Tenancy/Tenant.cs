using System;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Tenancy;

public sealed class Tenant : Entity<Guid>
{
    public const int MaxNameLength = 200;

    public Tenant(Guid id, string name, TenantStatus status)
        : base(ValidateId(id))
    {
        Name = ValidateName(name);
        Status = ValidateStatus(status);
    }

    public string Name { get; private set; }

    public TenantStatus Status { get; private set; }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier is required.", nameof(id));
        }

        return id;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tenant name is required.", nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Tenant name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return normalizedName;
    }

    private static TenantStatus ValidateStatus(TenantStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Tenant status is not defined.");
        }

        return status;
    }
}
