using System;
using System.Collections.Generic;
using System.Text;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Students;

public sealed class Branch : Entity<Guid>, ITenantOwnedEntity
{
    public const int MaxNameLength = 150;

    public Branch(Guid id, Guid tenantId, string name)
        : base(ValidateId(id))
    {
        TenantId = ValidateTenantId(tenantId);
        Name = ValidateName(name);
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public void Rename(string name)
    {
        Name = ValidateName(name);
    }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Branch identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Branch must belong to a tenant.", nameof(tenantId));
        }

        return tenantId;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Branch name is required.", nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Branch name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return normalizedName;
    }
}
