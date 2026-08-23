using System;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class TenantContextTests
{
    [Fact]
    public void Tenant_id_throws_until_a_trusted_tenant_is_established()
    {
        var context = new TenantContext();

        Assert.False(context.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => context.TenantId);
    }

    [Fact]
    public void Tenant_can_be_established_exactly_once()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        context.Establish(tenantId);

        Assert.True(context.IsAvailable);
        Assert.Equal(tenantId, context.TenantId);
        Assert.Throws<InvalidOperationException>(() => context.Establish(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new TenantContext().Establish(Guid.Empty));
    }

    [Fact]
    public void Separate_request_contexts_do_not_share_selection()
    {
        var firstRequest = new TenantContext();
        var secondRequest = new TenantContext();

        firstRequest.Establish(Guid.NewGuid());

        Assert.True(firstRequest.IsAvailable);
        Assert.False(secondRequest.IsAvailable);
    }
}
