using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeacherOS.Api.Authorization;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Authorization;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Invitations;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Authorization;
using TeacherOS.Infrastructure.Identity;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class OwnerPermissionBackfillTests
{
    [Fact]
    public async Task Old_owner_without_members_manage_is_upgraded_by_migration_while_custom_roles_remain_unchanged()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        using var sp = CreateServiceProvider(db.ConnectionString);
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var permissionResolver = scope.ServiceProvider.GetRequiredService<IPermissionResolver>();
        var authHandler = scope.ServiceProvider.GetRequiredService<PermissionAuthorizationHandler>();
        var currentUser = (TestCurrentUser)scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var tenantContext = (TestTenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>();

        // 1. Setup Tenant and Users
        var tenant = new Tenant(Guid.NewGuid(), "Legacy Academy", TenantStatus.Active);
        dbContext.Tenants.Add(tenant);

        var ownerUserId = Guid.NewGuid();
        var customUserId = Guid.NewGuid();

        var ownerUser = new ApplicationUser
        {
            Id = ownerUserId,
            UserName = "owner@legacy.local",
            NormalizedUserName = "OWNER@LEGACY.LOCAL",
            Email = "owner@legacy.local",
            NormalizedEmail = "OWNER@LEGACY.LOCAL",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        var customUser = new ApplicationUser
        {
            Id = customUserId,
            UserName = "teacher@legacy.local",
            NormalizedUserName = "TEACHER@LEGACY.LOCAL",
            Email = "teacher@legacy.local",
            NormalizedEmail = "TEACHER@LEGACY.LOCAL",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        dbContext.Users.AddRange(ownerUser, customUser);

        var oldOwnerPermissionSet = new[]
        {
            Permission.AttendanceRecord,
            Permission.PaymentRecord,
            Permission.PaymentAdjust,
            Permission.SessionClose,
            Permission.ShiftClose,
            Permission.ContentPublish,
        };

        var customRolePermissionSet = new[]
        {
            Permission.AttendanceRecord,
            Permission.SessionClose,
        };

        // 2. Insert Old Owner Role directly in SQL with old permission set (pre-members.manage)
        var oldOwnerRole = new Role(Guid.NewGuid(), tenant.Id, "Owner", oldOwnerPermissionSet);
        var customRole = new Role(Guid.NewGuid(), tenant.Id, "Teacher", customRolePermissionSet);

        dbContext.Roles.AddRange(oldOwnerRole, customRole);

        var ownerMembership = new TenantMembership(
            Guid.NewGuid(),
            tenant.Id,
            ownerUserId,
            TenantMembershipStatus.Active,
            oldOwnerRole.Id);

        var customMembership = new TenantMembership(
            Guid.NewGuid(),
            tenant.Id,
            customUserId,
            TenantMembershipStatus.Active,
            customRole.Id);

        dbContext.TenantMemberships.AddRange(ownerMembership, customMembership);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 3. REGRESSION CHECK: Before backfill, Old Owner CANNOT satisfy members.manage
        currentUser.SetUser(ownerUserId, "owner@legacy.local");
        tenantContext.Establish(tenant.Id);

        var preBackfillOwnerPerms = await permissionResolver.GetPermissionsAsync(ownerUserId, tenant.Id, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Permission.MembersManage, preBackfillOwnerPerms);

        var preBackfillReq = new PermissionRequirement(Permission.MembersManage);
        var preBackfillAuthContext = new AuthorizationHandlerContext(
            [preBackfillReq],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerUserId.ToString())], "TestAuth")),
            null);

        await authHandler.HandleAsync(preBackfillAuthContext);
        Assert.False(preBackfillAuthContext.HasSucceeded, "Old Owner should NOT satisfy members.manage before upgrade.");

        // 4. EXECUTE BACKFILL MIGRATION SQL
        await dbContext.Database.ExecuteSqlRawAsync(@"
            UPDATE [Roles]
            SET [PermissionCodes] = JSON_MODIFY([PermissionCodes], 'append $', 'members.manage')
            WHERE [Name] = 'Owner'
              AND ([PermissionCodes] NOT LIKE '%""members.manage""%' OR [PermissionCodes] IS NULL);
        ", TestContext.Current.CancellationToken);

        // Detach to force fresh read from SQL
        dbContext.ChangeTracker.Clear();

        // 5. POST-BACKFILL CHECK: Old Owner NOW HAS members.manage and SATISFIES authorization
        var postBackfillOwnerPerms = await permissionResolver.GetPermissionsAsync(ownerUserId, tenant.Id, TestContext.Current.CancellationToken);
        Assert.Contains(Permission.MembersManage, postBackfillOwnerPerms);

        // Verify all 6 original permissions are preserved
        foreach (var originalPerm in oldOwnerPermissionSet)
        {
            Assert.Contains(originalPerm, postBackfillOwnerPerms);
        }
        Assert.Equal(oldOwnerPermissionSet.Length + 1, postBackfillOwnerPerms.Count);

        var postBackfillAuthContext = new AuthorizationHandlerContext(
            [new PermissionRequirement(Permission.MembersManage)],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerUserId.ToString())], "TestAuth")),
            null);

        await authHandler.HandleAsync(postBackfillAuthContext);
        Assert.True(postBackfillAuthContext.HasSucceeded, "Old Owner MUST satisfy members.manage after upgrade.");

        // IDEMPOTENCY CHECK: Re-running backfill SQL does not duplicate permissions or modify data
        await dbContext.Database.ExecuteSqlRawAsync(@"
            UPDATE [Roles]
            SET [PermissionCodes] = JSON_MODIFY([PermissionCodes], 'append $', 'members.manage')
            WHERE [Name] = 'Owner'
              AND ([PermissionCodes] NOT LIKE '%""members.manage""%' OR [PermissionCodes] IS NULL);
        ", TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();

        var recheckOwnerPerms = await permissionResolver.GetPermissionsAsync(ownerUserId, tenant.Id, TestContext.Current.CancellationToken);
        Assert.Equal(oldOwnerPermissionSet.Length + 1, recheckOwnerPerms.Count);
        Assert.Equal(1, recheckOwnerPerms.Count(p => p == Permission.MembersManage));

        // 6. CUSTOM ROLE CHECK: Custom role did NOT gain members.manage
        var customRolePerms = await permissionResolver.GetPermissionsAsync(customUserId, tenant.Id, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Permission.MembersManage, customRolePerms);

        currentUser.SetUser(customUserId, "teacher@legacy.local");
        var customAuthContext = new AuthorizationHandlerContext(
            [new PermissionRequirement(Permission.MembersManage)],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customUserId.ToString())], "TestAuth")),
            null);

        await authHandler.HandleAsync(customAuthContext);
        Assert.False(customAuthContext.HasSucceeded, "Custom role must remain unauthorized for members.manage.");
    }

    [Fact]
    public async Task Existing_legacy_owner_invitation_creation_flow_succeeds_after_upgrade()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        using var sp = CreateServiceProvider(db.ConnectionString);
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inviteHandler = scope.ServiceProvider.GetRequiredService<CreateTenantInvitationHandler>();
        var currentUser = (TestCurrentUser)scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var tenantContext = (TestTenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var tenant = new Tenant(Guid.NewGuid(), "Legacy Academy 2", TenantStatus.Active);
        dbContext.Tenants.Add(tenant);

        var ownerUserId = Guid.NewGuid();
        var ownerUser = new ApplicationUser
        {
            Id = ownerUserId,
            UserName = "owner2@legacy.local",
            NormalizedUserName = "OWNER2@LEGACY.LOCAL",
            Email = "owner2@legacy.local",
            NormalizedEmail = "OWNER2@LEGACY.LOCAL",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        dbContext.Users.Add(ownerUser);

        var oldOwnerPermissionSet = new[]
        {
            Permission.AttendanceRecord,
            Permission.PaymentRecord,
            Permission.PaymentAdjust,
            Permission.SessionClose,
            Permission.ShiftClose,
            Permission.ContentPublish,
        };

        var oldOwnerRole = new Role(Guid.NewGuid(), tenant.Id, "Owner", oldOwnerPermissionSet);
        dbContext.Roles.Add(oldOwnerRole);

        var ownerMembership = new TenantMembership(
            Guid.NewGuid(),
            tenant.Id,
            ownerUserId,
            TenantMembershipStatus.Active,
            oldOwnerRole.Id);
        dbContext.TenantMemberships.Add(ownerMembership);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Apply backfill migration SQL
        await dbContext.Database.ExecuteSqlRawAsync(@"
            UPDATE [Roles]
            SET [PermissionCodes] = JSON_MODIFY([PermissionCodes], 'append $', 'members.manage')
            WHERE [Name] = 'Owner'
              AND ([PermissionCodes] NOT LIKE '%""members.manage""%' OR [PermissionCodes] IS NULL);
        ", TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();

        currentUser.SetUser(ownerUserId, "owner2@legacy.local");
        tenantContext.Establish(tenant.Id);

        var inviteCommand = new CreateTenantInvitationCommand(
            tenant.Id,
            "newteacher@legacy.local",
            null);

        var inviteResult = await inviteHandler.HandleAsync(inviteCommand, TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess, $"Invitation creation should succeed. Error: {inviteResult.Error?.Description}");
        Assert.NotEqual(Guid.Empty, inviteResult.Value.InvitationId);
    }

    [Fact]
    public async Task Tenant_isolation_prevents_role_in_tenant_a_from_authorizing_tenant_b()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        using var sp = CreateServiceProvider(db.ConnectionString);
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var permissionResolver = scope.ServiceProvider.GetRequiredService<IPermissionResolver>();

        var tenantA = new Tenant(Guid.NewGuid(), "Academy A", TenantStatus.Active);
        var tenantB = new Tenant(Guid.NewGuid(), "Academy B", TenantStatus.Active);
        dbContext.Tenants.AddRange(tenantA, tenantB);

        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.local",
            NormalizedUserName = "USER@TEST.LOCAL",
            Email = "user@test.local",
            NormalizedEmail = "USER@TEST.LOCAL",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        dbContext.Users.Add(user);

        var ownerRoleA = new Role(Guid.NewGuid(), tenantA.Id, "Owner", Permission.All);
        dbContext.Roles.Add(ownerRoleA);

        var membershipA = new TenantMembership(
            Guid.NewGuid(),
            tenantA.Id,
            userId,
            TenantMembershipStatus.Active,
            ownerRoleA.Id);

        dbContext.TenantMemberships.Add(membershipA);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // User has permissions in Tenant A
        var permsA = await permissionResolver.GetPermissionsAsync(userId, tenantA.Id, TestContext.Current.CancellationToken);
        Assert.Contains(Permission.MembersManage, permsA);

        // User has NO permissions in Tenant B
        var permsB = await permissionResolver.GetPermissionsAsync(userId, tenantB.Id, TestContext.Current.CancellationToken);
        Assert.Empty(permsB);
    }

    [Fact]
    public async Task New_registered_owner_receives_all_permissions_including_members_manage()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        using var sp = CreateServiceProvider(db.ConnectionString);
        using var scope = sp.CreateScope();
        var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterHandler>();
        var permissionResolver = scope.ServiceProvider.GetRequiredService<IPermissionResolver>();

        var email = $"new_owner_{Guid.NewGuid():N}@test.local";
        var result = await registerHandler.HandleAsync(
            new RegisterCommand(email, "Password123!", "Modern Academy"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        var perms = await permissionResolver.GetPermissionsAsync(result.Value.UserId, result.Value.TenantId, TestContext.Current.CancellationToken);
        Assert.Contains(Permission.MembersManage, perms);
        Assert.Equal(Permission.All.Count, perms.Count);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                ["Brevo:ApiKey"] = "dummy-api-key",
                ["Brevo:SenderEmail"] = "noreply@teacheros.test",
                ["Brevo:SenderName"] = "TeacherOS",
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration);

        services.AddScoped<ICurrentUser, TestCurrentUser>();
        services.AddScoped<ITenantContext, TestTenantContext>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<PermissionAuthorizationHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<CreateTenantInvitationHandler>();
        services.AddScoped<ITenantInvitationStore, TenantInvitationStore>();
        services.AddScoped<ITenantMembershipManagementStore, TenantMembershipManagementStore>();
        services.AddScoped<ITransactionalEmailSender, TestEmailSender>();

        return services.BuildServiceProvider();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated { get; private set; }
        public Guid? UserId { get; private set; }
        public string? Email { get; private set; }

        public void SetUser(Guid userId, string email)
        {
            IsAuthenticated = true;
            UserId = userId;
            Email = email;
        }

        public void Clear()
        {
            IsAuthenticated = false;
            UserId = null;
            Email = null;
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; private set; }
        public bool IsAvailable { get; private set; }

        public void Establish(Guid tenantId)
        {
            TenantId = tenantId;
            IsAvailable = true;
        }
    }

    private sealed class TestEmailSender : ITransactionalEmailSender
    {
        public Task<EmailDispatchResult> SendInvitationEmailAsync(
            InvitationEmailRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(EmailDispatchResult.Success("test-msg-id"));
        }
    }
}
