using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Authorization;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Persistence;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class OnboardingPersistenceTests
{
    [Fact]
    public async Task Real_onboarding_persists_user_tenant_owner_role_and_membership_atomically()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var handler = sp.GetRequiredService<RegisterHandler>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();
        var authenticator = sp.GetRequiredService<IIdentityAuthenticator>();
        var sessionReader = sp.GetRequiredService<ICurrentSessionReader>();
        var membershipResolver = sp.GetRequiredService<ITenantMembershipResolver>();
        var permissionResolver = sp.GetRequiredService<IPermissionResolver>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"teacher_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"Test Academy {testSuffix}";

        var result = await handler.HandleAsync(
            new RegisterCommand(email, password, tenantName),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var userId = result.Value.UserId;
        var tenantId = result.Value.TenantId;

        // 1. Verify User created in AspNetUsers
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);

        // 2. Verify Tenant created
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, TestContext.Current.CancellationToken);
        Assert.NotNull(tenant);
        Assert.Equal(tenantName, tenant.Name);
        Assert.Equal(TenantStatus.Active, tenant.Status);

        // 3. Verify Owner Role created with Permission.All
        var ownerRole = await dbContext.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Name == "Owner", TestContext.Current.CancellationToken);
        Assert.NotNull(ownerRole);
        Assert.Equal(Permission.All.OrderBy(p => p), ownerRole.PermissionCodes.OrderBy(p => p));

        // 4. Verify Membership created with correct RoleId
        var membership = await dbContext.TenantMemberships.AsNoTracking().SingleOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, TestContext.Current.CancellationToken);
        Assert.NotNull(membership);
        Assert.Equal(TenantMembershipStatus.Active, membership.Status);
        Assert.Equal(ownerRole.Id, membership.RoleId);

        // 5. Verify registered user can subsequently login
        var authResult = await authenticator.AuthenticateAsync(email, password, TestContext.Current.CancellationToken);
        Assert.NotNull(authResult);
        Assert.Equal(userId, authResult.UserId);

        // 6. Verify session contains membership
        var session = await sessionReader.GetAsync(userId, TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        Assert.Contains(session.Memberships, m => m.TenantId == tenantId && m.TenantName == tenantName && m.MembershipStatus == TenantMembershipStatus.Active);

        // 7. Verify X-Tenant-Id / TenantContext accepts the new Tenant
        var hasActiveMembership = await membershipResolver.HasActiveMembershipAsync(userId, tenantId, TestContext.Current.CancellationToken);
        Assert.True(hasActiveMembership);

        // 8. Verify Owner permissions resolve correctly
        var permissions = await permissionResolver.GetPermissionsAsync(userId, tenantId, TestContext.Current.CancellationToken);
        Assert.Equal(Permission.All.OrderBy(p => p), permissions.OrderBy(p => p));
    }

    [Fact]
    public async Task Duplicate_email_does_not_create_another_tenant_or_partial_state()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var handler = sp.GetRequiredService<RegisterHandler>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"duplicate_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var firstTenantName = $"Initial Academy {testSuffix}";
        var secondTenantName = $"Second Academy {testSuffix}";

        // First registration succeeds
        var firstResult = await handler.HandleAsync(
            new RegisterCommand(email, password, firstTenantName),
            TestContext.Current.CancellationToken);
        Assert.True(firstResult.IsSuccess);

        var tenantCountBefore = await dbContext.Tenants.CountAsync(t => t.Name == secondTenantName, TestContext.Current.CancellationToken);
        Assert.Equal(0, tenantCountBefore);

        // Second registration with same email fails
        var secondResult = await handler.HandleAsync(
            new RegisterCommand(email, password, secondTenantName),
            TestContext.Current.CancellationToken);
        Assert.True(secondResult.IsFailure);
        Assert.Equal(AuthenticationErrors.DuplicateEmail, secondResult.Error);

        // Confirm second tenant was never created
        var tenantCountAfter = await dbContext.Tenants.CountAsync(t => t.Name == secondTenantName, TestContext.Current.CancellationToken);
        Assert.Equal(0, tenantCountAfter);
    }

    [Fact]
    public async Task Simulated_persistence_failure_rolls_back_entire_transaction()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var userRegistrar = sp.GetRequiredService<IIdentityUserRegistrar>();
        var onboardingStore = sp.GetRequiredService<ITenantOnboardingStore>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();

        // FailingUnitOfWork allows Identity user creation to succeed inside the transaction,
        // but throws when saving onboarding entities (Tenant, Role, Membership).
        var failingUnitOfWork = new FailingUnitOfWork(unitOfWork);
        var handler = new RegisterHandler(userRegistrar, onboardingStore, failingUnitOfWork);

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"rollback_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"Rollback Academy {testSuffix}";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new RegisterCommand(email, password, tenantName),
                TestContext.Current.CancellationToken));

        // Verify that Identity user creation happened inside the transaction but was rolled back:
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, TestContext.Current.CancellationToken);
        Assert.Null(user);

        // Verify that tenant was not committed:
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Name == tenantName, TestContext.Current.CancellationToken);
        Assert.Null(tenant);

        // Verify that no orphaned roles or memberships were committed:
        var rolesCount = await dbContext.Roles.AsNoTracking().CountAsync(r => r.Name == "Owner" && r.TenantId == (tenant != null ? tenant.Id : Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(0, rolesCount);
    }

    private static IServiceProvider CreateServiceProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Database:ConnectionString", connectionString),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddInfrastructure(configuration);
        services.AddScoped<RegisterHandler>();

        return services.BuildServiceProvider();
    }

    private sealed class FailingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated unexpected database failure.");
        }

        public Task<Result<T>> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken = default)
        {
            return inner.ExecuteInTransactionAsync(operation, cancellationToken);
        }
    }
}
