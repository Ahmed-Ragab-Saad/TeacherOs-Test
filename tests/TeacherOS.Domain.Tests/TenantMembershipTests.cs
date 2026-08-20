using System;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Domain.Tests;

public sealed class TenantMembershipTests
{
    [Theory]
    [InlineData(TenantMembershipStatus.Active)]
    [InlineData(TenantMembershipStatus.Suspended)]
    public void Membership_accepts_each_defined_status(TenantMembershipStatus status)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var membership = new TenantMembership(Guid.NewGuid(), tenantId, userId, status);

        Assert.Equal(tenantId, membership.TenantId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(status, membership.Status);
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("tenant")]
    [InlineData("user")]
    public void Membership_rejects_an_empty_boundary_identifier(string identifier)
    {
        var membershipId = identifier == "membership" ? Guid.Empty : Guid.NewGuid();
        var tenantId = identifier == "tenant" ? Guid.Empty : Guid.NewGuid();
        var userId = identifier == "user" ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => new TenantMembership(
                membershipId,
                tenantId,
                userId,
                TenantMembershipStatus.Active));
    }

    [Fact]
    public void Membership_rejects_an_undefined_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TenantMembership(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                (TenantMembershipStatus)999));
    }
}
