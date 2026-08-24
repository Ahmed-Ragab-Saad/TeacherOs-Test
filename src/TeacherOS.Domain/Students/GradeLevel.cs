using System;
using System.Collections.Generic;
using System.Text;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Students;

public sealed class GradeLevel : Entity<Guid>, ITenantOwnedEntity
{
    public const int MaxNameLength = 150;

    public GradeLevel(Guid id, Guid tenantId, string name, int sortOrder)
        : base(ValidateId(id))
    {
        TenantId = ValidateTenantId(tenantId);
        Name = ValidateName(name);
        SortOrder = ValidateSortOrder(sortOrder);
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(string name, int sortOrder)
    {
        Name = ValidateName(name);
        SortOrder = ValidateSortOrder(sortOrder);
    }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Grade level identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Grade level must belong to a tenant.", nameof(tenantId));
        }

        return tenantId;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Grade level name is required.", nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Grade level name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return normalizedName;
    }

    private static int ValidateSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Sort order cannot be negative.");
        }

        return sortOrder;
    }
}
