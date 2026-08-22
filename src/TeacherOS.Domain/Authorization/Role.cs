using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeacherOS.Domain.Common;

namespace TeacherOS.Domain.Authorization;

public sealed class Role : Entity<Guid>
{
    public const int MaxNameLength = 100;

    public Role(Guid id, Guid tenantId, string name, IReadOnlyCollection<string> permissionCodes)
        : base(ValidateId(id))
    {
        TenantId = ValidateTenantId(tenantId);
        Name = ValidateName(name);
        PermissionCodes = ValidatePermissionCodes(permissionCodes);
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public IReadOnlyCollection<string> PermissionCodes { get; private set; }

    private static Guid ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(id));
        }

        return id;
    }

    private static Guid ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Role must belong to a tenant.", nameof(tenantId));
        }

        return tenantId;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name is required.", nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Role name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return normalizedName;
    }

    private static IReadOnlyCollection<string> ValidatePermissionCodes(IReadOnlyCollection<string> permissionCodes)
    {
        var codes = permissionCodes?.Distinct(StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(permissionCodes));

        if (codes.Length == 0)
        {
            throw new ArgumentException("A role must grant at least one permission.", nameof(permissionCodes));
        }

        var unknownCode = codes.FirstOrDefault(code => !Permission.All.Contains(code, StringComparer.Ordinal));

        if (unknownCode is not null)
        {
            throw new ArgumentException(
                $"'{unknownCode}' is not a recognized permission code.",
                nameof(permissionCodes));
        }

        return codes;
    }
}
