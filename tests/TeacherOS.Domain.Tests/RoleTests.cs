using System;
using TeacherOS.Domain.Authorization;
using Xunit;

namespace TeacherOS.Domain.Tests;

public sealed class RoleTests
{
    [Fact]
    public void Valid_owner_role_accepts_permission_all()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var role = new Role(roleId, tenantId, "Owner", Permission.All);

        Assert.Equal(roleId, role.Id);
        Assert.Equal(tenantId, role.TenantId);
        Assert.Equal("Owner", role.Name);
        Assert.Equal(Permission.All, role.PermissionCodes);
    }

    [Fact]
    public void Role_accepts_custom_valid_permissions_and_normalizes_whitespace_in_name()
    {
        var tenantId = Guid.NewGuid();
        var role = new Role(Guid.NewGuid(), tenantId, "  Custom Role  ", [Permission.AttendanceRecord, Permission.PaymentRecord]);

        Assert.Equal("Custom Role", role.Name);
        Assert.Equal(2, role.PermissionCodes.Count);
        Assert.Contains(Permission.AttendanceRecord, role.PermissionCodes);
        Assert.Contains(Permission.PaymentRecord, role.PermissionCodes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Role_rejects_missing_name(string? name)
    {
        var tenantId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Role(Guid.NewGuid(), tenantId, name!, Permission.All));
    }

    [Fact]
    public void Role_rejects_name_exceeding_max_length()
    {
        var tenantId = Guid.NewGuid();
        var tooLongName = new string('A', Role.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => new Role(Guid.NewGuid(), tenantId, tooLongName, Permission.All));
    }

    [Fact]
    public void Role_rejects_empty_role_identifier()
    {
        var tenantId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Role(Guid.Empty, tenantId, "Owner", Permission.All));
    }

    [Fact]
    public void Role_rejects_empty_tenant_identifier()
    {
        Assert.Throws<ArgumentException>(() => new Role(Guid.NewGuid(), Guid.Empty, "Owner", Permission.All));
    }

    [Fact]
    public void Role_rejects_null_permissions()
    {
        var tenantId = Guid.NewGuid();
        Assert.Throws<ArgumentNullException>(() => new Role(Guid.NewGuid(), tenantId, "Owner", null!));
    }

    [Fact]
    public void Role_rejects_empty_permissions()
    {
        var tenantId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Role(Guid.NewGuid(), tenantId, "Owner", Array.Empty<string>()));
    }

    [Fact]
    public void Role_rejects_unrecognized_permission_code()
    {
        var tenantId = Guid.NewGuid();
        var ex = Assert.Throws<ArgumentException>(
            () => new Role(Guid.NewGuid(), tenantId, "Owner", ["invalid.permission.code"]));

        Assert.Contains("invalid.permission.code", ex.Message, StringComparison.Ordinal);
    }
}
