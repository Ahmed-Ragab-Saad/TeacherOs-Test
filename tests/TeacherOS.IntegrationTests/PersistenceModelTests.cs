using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Identity;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Infrastructure_registers_the_standard_identity_user_store()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(
                    "Database:ConnectionString",
                    "Server=localhost;Database=TeacherOSModelTests;Integrated Security=true;Encrypt=false"),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IUserStore<ApplicationUser>>());
    }

    [Fact]
    public void Identity_and_tenancy_share_one_relational_model_without_a_user_tenant_shortcut()
    {
        using var dbContext = CreateDbContext();
        var userEntity = dbContext.Model.FindEntityType(typeof(ApplicationUser))
            ?? throw new InvalidOperationException("ApplicationUser mapping was not found.");

        Assert.Equal("AspNetUsers", userEntity.GetTableName());
        Assert.Null(userEntity.FindProperty("TenantId"));
        Assert.Equal(
            ValueGenerated.Never,
            userEntity.FindProperty(nameof(ApplicationUser.Id))?.ValueGenerated);
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(Tenant)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(TenantMembership)));
        Assert.DoesNotContain(
            dbContext.Model.GetEntityTypes(),
            entityType => entityType.ClrType.Name.StartsWith("IdentityRole", StringComparison.Ordinal));
    }

    [Fact]
    public void Tenant_mapping_bounds_the_required_name_and_stores_a_required_integer_status()
    {
        using var dbContext = CreateDbContext();
        var tenantEntity = dbContext.Model.FindEntityType(typeof(Tenant))
            ?? throw new InvalidOperationException("Tenant mapping was not found.");
        var nameProperty = tenantEntity.FindProperty(nameof(Tenant.Name))
            ?? throw new InvalidOperationException("Tenant name mapping was not found.");
        var statusProperty = tenantEntity.FindProperty(nameof(Tenant.Status))
            ?? throw new InvalidOperationException("Tenant status mapping was not found.");
        var designTimeTenantEntity = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(Tenant))
            ?? throw new InvalidOperationException("Design-time Tenant mapping was not found.");

        Assert.Equal("Tenants", tenantEntity.GetTableName());
        Assert.False(nameProperty.IsNullable);
        Assert.Equal(Tenant.MaxNameLength, nameProperty.GetMaxLength());
        Assert.False(statusProperty.IsNullable);
        Assert.Equal("int", statusProperty.GetRelationalTypeMapping().StoreType);
        Assert.Contains(
            designTimeTenantEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Tenants_Status_Valid");
    }

    [Fact]
    public void Membership_mapping_is_unique_per_boundary_and_restricts_both_foreign_keys()
    {
        using var dbContext = CreateDbContext();
        var membershipEntity = dbContext.Model.FindEntityType(typeof(TenantMembership))
            ?? throw new InvalidOperationException("TenantMembership mapping was not found.");
        var boundaryIndex = membershipEntity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(TenantMembership.TenantId), nameof(TenantMembership.UserId)]));
        var tenantForeignKey = membershipEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Tenant));
        var userForeignKey = membershipEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser));

        Assert.True(boundaryIndex.IsUnique);
        Assert.Equal("UX_TenantMemberships_TenantId_UserId", boundaryIndex.GetDatabaseName());
        Assert.False(membershipEntity.FindProperty(nameof(TenantMembership.TenantId))!.IsNullable);
        Assert.False(membershipEntity.FindProperty(nameof(TenantMembership.UserId))!.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, tenantForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=TeacherOSModelTests;Integrated Security=true;Encrypt=false")
            .Options;

        return new ApplicationDbContext(options, new TenantContext());
    }
}
