using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using TeacherOS.Domain.Common;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class TenantIsolationFilterTests
{
    [Fact]
    public void Filter_denies_all_rows_when_no_tenant_is_established()
    {
        var tenantContext = new TenantContext();
        var filter = BuildFilter(tenantContext);

        var anyRow = new FakeTenantOwnedEntity(Guid.NewGuid());

        Assert.False(filter(anyRow));
    }

    [Fact]
    public void Filter_allows_only_rows_matching_the_established_tenant()
    {
        var tenantContext = new TenantContext();
        var establishedTenantId = Guid.NewGuid();
        tenantContext.Establish(establishedTenantId);
        var filter = BuildFilter(tenantContext);

        var matchingRow = new FakeTenantOwnedEntity(establishedTenantId);
        var otherTenantRow = new FakeTenantOwnedEntity(Guid.NewGuid());

        Assert.True(filter(matchingRow));
        Assert.False(filter(otherTenantRow));
    }

    private static Func<FakeTenantOwnedEntity, bool> BuildFilter(TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=TeacherOSFilterTests;Integrated Security=true;Encrypt=false")
            .Options;

        using var dbContext = new ApplicationDbContext(options, tenantContext);

        var method = typeof(ApplicationDbContext)
            .GetMethod("BuildFailClosedTenantFilter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(FakeTenantOwnedEntity));

        var lambda = (LambdaExpression)method.Invoke(dbContext, null)!;
        return (Func<FakeTenantOwnedEntity, bool>)lambda.Compile();
    }

    private sealed class FakeTenantOwnedEntity(Guid tenantId) : ITenantOwnedEntity
    {
        public Guid TenantId { get; } = tenantId;
    }
}
