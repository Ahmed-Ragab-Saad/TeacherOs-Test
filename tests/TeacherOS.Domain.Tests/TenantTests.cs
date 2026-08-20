using System;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Domain.Tests;

public sealed class TenantTests
{
    [Theory]
    [InlineData(TenantStatus.Trial)]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Expired)]
    [InlineData(TenantStatus.Closed)]
    public void Tenant_accepts_each_defined_status(TenantStatus status)
    {
        var tenant = new Tenant(Guid.NewGuid(), "  North Academy  ", status);

        Assert.Equal("North Academy", tenant.Name);
        Assert.Equal(status, tenant.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Tenant_rejects_a_missing_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), name!, TenantStatus.Trial));
    }

    [Fact]
    public void Tenant_rejects_a_name_beyond_the_supported_length()
    {
        var name = new string('A', Tenant.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), name, TenantStatus.Active));
    }

    [Fact]
    public void Tenant_rejects_an_empty_identifier()
    {
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.Empty, "North Academy", TenantStatus.Active));
    }

    [Fact]
    public void Tenant_rejects_an_undefined_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Tenant(Guid.NewGuid(), "North Academy", (TenantStatus)999));
    }
}
