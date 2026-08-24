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
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Invitations;
using TeacherOS.Application.Memberships;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Identity;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class TenantMembershipManagementPersistenceTests
{
    [Fact]
    public async Task Full_membership_lifecycle_and_final_owner_protection_with_real_sql()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var registerHandler = sp.GetRequiredService<RegisterHandler>();
        var inviteHandler = sp.GetRequiredService<CreateTenantInvitationHandler>();
        var acceptHandler = sp.GetRequiredService<AcceptTenantInvitationHandler>();
        var listMembersHandler = sp.GetRequiredService<ListTenantMembersHandler>();
        var updateStatusHandler = sp.GetRequiredService<UpdateTenantMembershipStatusHandler>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var testCurrentUser = (TestCurrentUser)sp.GetRequiredService<ICurrentUser>();
        var emailSender = (TestEmailSender)sp.GetRequiredService<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"Academy {testSuffix}";

        // 1. Owner registers tenant
        var regResult = await registerHandler.HandleAsync(
            new RegisterCommand(ownerEmail, password, tenantName),
            TestContext.Current.CancellationToken);
        Assert.True(regResult.IsSuccess);
        var ownerUserId = regResult.Value.UserId;
        var tenantId = regResult.Value.TenantId;

        // Establish tenant context & current user as Owner
        tenantContext.Establish(tenantId);
        testCurrentUser.SetUser(ownerUserId, ownerEmail);

        // 2. List members -> contains Owner
        var membersResult = await listMembersHandler.HandleAsync(
            new ListTenantMembersQuery(tenantId),
            TestContext.Current.CancellationToken);
        Assert.True(membersResult.IsSuccess);
        Assert.Single(membersResult.Value);
        Assert.Equal(ownerEmail, membersResult.Value[0].Email);
        Assert.Equal("Active", membersResult.Value[0].Status);
        Assert.Equal("Owner", membersResult.Value[0].RoleName);
        var ownerMembershipId = membersResult.Value[0].MembershipId;

        // 3. Final Owner Protection: Attempting to suspend the only owner is rejected
        var suspendOwnerResult = await updateStatusHandler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(tenantId, ownerMembershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);
        Assert.True(suspendOwnerResult.IsFailure);
        Assert.Equal(MembershipErrors.CannotDisableLastOwner, suspendOwnerResult.Error);

        // 4. Invite a staff member (without role)
        var memberEmail = $"member_{testSuffix}@test-academy.local";
        var inviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, memberEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess);

        // Capture raw token from sent email
        var capturedEmail = emailSender.SentRequests.LastOrDefault(r => r.RecipientEmail == memberEmail);
        Assert.NotNull(capturedEmail);
        var rawToken = capturedEmail.RawInvitationToken;

        // 5. Member accepts invitation as a new user
        testCurrentUser.Clear();
        var acceptResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(rawToken, "MemberPassword@2026!"),
            TestContext.Current.CancellationToken);
        Assert.True(acceptResult.IsSuccess);
        Assert.True(acceptResult.Value.IsNewUser);
        var memberUserId = acceptResult.Value.UserId;

        // Switch back to Owner
        testCurrentUser.SetUser(ownerUserId, ownerEmail);

        // 6. List members -> contains Owner and new Member
        var membersAfterAccept = await listMembersHandler.HandleAsync(
            new ListTenantMembersQuery(tenantId),
            TestContext.Current.CancellationToken);
        Assert.True(membersAfterAccept.IsSuccess);
        Assert.Equal(2, membersAfterAccept.Value.Count);

        var memberItem = membersAfterAccept.Value.Single(m => m.UserId == memberUserId);
        Assert.Equal("Active", memberItem.Status);
        Assert.Null(memberItem.RoleName); // Role-less membership supported!

        // 7. Owner suspends the new member -> succeeds
        var suspendMemberResult = await updateStatusHandler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(tenantId, memberItem.MembershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);
        Assert.True(suspendMemberResult.IsSuccess);

        var memberAfterSuspend = await dbContext.TenantMemberships.AsNoTracking().SingleAsync(m => m.Id == memberItem.MembershipId, TestContext.Current.CancellationToken);
        Assert.Equal(TenantMembershipStatus.Suspended, memberAfterSuspend.Status);

        // 8. Owner reactivates the member -> succeeds
        var reactivateResult = await updateStatusHandler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(tenantId, memberItem.MembershipId, TenantMembershipStatus.Active),
            TestContext.Current.CancellationToken);
        Assert.True(reactivateResult.IsSuccess);

        var memberAfterReactivate = await dbContext.TenantMemberships.AsNoTracking().SingleAsync(m => m.Id == memberItem.MembershipId, TestContext.Current.CancellationToken);
        Assert.Equal(TenantMembershipStatus.Active, memberAfterReactivate.Status);
    }

    [Fact]
    public async Task Two_owners_scenario_allows_suspending_one_owner()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var registerHandler = sp.GetRequiredService<RegisterHandler>();
        var inviteHandler = sp.GetRequiredService<CreateTenantInvitationHandler>();
        var acceptHandler = sp.GetRequiredService<AcceptTenantInvitationHandler>();
        var listMembersHandler = sp.GetRequiredService<ListTenantMembersHandler>();
        var updateStatusHandler = sp.GetRequiredService<UpdateTenantMembershipStatusHandler>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var testCurrentUser = (TestCurrentUser)sp.GetRequiredService<ICurrentUser>();
        var emailSender = (TestEmailSender)sp.GetRequiredService<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var owner1Email = $"owner1_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"CoOwned Academy {testSuffix}";

        // Register initial Owner
        var regResult = await registerHandler.HandleAsync(
            new RegisterCommand(owner1Email, password, tenantName),
            TestContext.Current.CancellationToken);
        Assert.True(regResult.IsSuccess);
        var tenantId = regResult.Value.TenantId;
        var owner1UserId = regResult.Value.UserId;

        tenantContext.Establish(tenantId);
        testCurrentUser.SetUser(owner1UserId, owner1Email);

        var ownerRole = await dbContext.Roles.AsNoTracking().SingleAsync(r => r.TenantId == tenantId && r.Name == "Owner", TestContext.Current.CancellationToken);

        // Invite 2nd Owner with Owner Role
        var owner2Email = $"owner2_{testSuffix}@test-academy.local";
        var inviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, owner2Email, ownerRole.Id),
            TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess);

        var rawToken = emailSender.SentRequests.Last(r => r.RecipientEmail == owner2Email).RawInvitationToken;

        // Owner 2 accepts invitation
        testCurrentUser.Clear();
        var acceptResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(rawToken, "Owner2Password@2026!"),
            TestContext.Current.CancellationToken);
        Assert.True(acceptResult.IsSuccess);
        var owner2UserId = acceptResult.Value.UserId;

        // Switch back to Owner 1
        testCurrentUser.SetUser(owner1UserId, owner1Email);

        var members = await listMembersHandler.HandleAsync(new ListTenantMembersQuery(tenantId), TestContext.Current.CancellationToken);
        var owner2Membership = members.Value.Single(m => m.UserId == owner2UserId);

        // Suspending Owner 2 succeeds because Owner 1 remains active
        var suspendOwner2Result = await updateStatusHandler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(tenantId, owner2Membership.MembershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);
        Assert.True(suspendOwner2Result.IsSuccess);

        // Now only 1 active owner remains. Attempting to suspend Owner 1 must be rejected!
        var owner1Membership = members.Value.Single(m => m.UserId == owner1UserId);
        var suspendOwner1Result = await updateStatusHandler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(tenantId, owner1Membership.MembershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);
        Assert.True(suspendOwner1Result.IsFailure);
        Assert.Equal(MembershipErrors.CannotDisableLastOwner, suspendOwner1Result.Error);
    }

    [Fact]
    public async Task Concurrent_deactivation_of_two_owners_prevents_zero_owners_invariant_violation()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);

        // 1. Setup tenant with 2 active owners
        using var setupScope = services.CreateScope();
        var registerHandler = setupScope.ServiceProvider.GetRequiredService<RegisterHandler>();
        var inviteHandler = setupScope.ServiceProvider.GetRequiredService<CreateTenantInvitationHandler>();
        var acceptHandler = setupScope.ServiceProvider.GetRequiredService<AcceptTenantInvitationHandler>();
        var listMembersHandler = setupScope.ServiceProvider.GetRequiredService<ListTenantMembersHandler>();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantContext = setupScope.ServiceProvider.GetRequiredService<ITenantContext>();
        var testCurrentUser = (TestCurrentUser)setupScope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var emailSender = (TestEmailSender)setupScope.ServiceProvider.GetRequiredService<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var owner1Email = $"race_owner1_{testSuffix}@test-academy.local";
        var owner2Email = $"race_owner2_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"Race Academy {testSuffix}";

        var regResult = await registerHandler.HandleAsync(
            new RegisterCommand(owner1Email, password, tenantName),
            TestContext.Current.CancellationToken);
        Assert.True(regResult.IsSuccess);
        var tenantId = regResult.Value.TenantId;
        var owner1UserId = regResult.Value.UserId;

        tenantContext.Establish(tenantId);
        testCurrentUser.SetUser(owner1UserId, owner1Email);

        var ownerRole = await dbContext.Roles.AsNoTracking().SingleAsync(r => r.TenantId == tenantId && r.Name == "Owner", TestContext.Current.CancellationToken);

        var inviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, owner2Email, ownerRole.Id),
            TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess);

        var rawToken = emailSender.SentRequests.Last(r => r.RecipientEmail == owner2Email).RawInvitationToken;

        testCurrentUser.Clear();
        var acceptResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(rawToken, "Owner2Password@2026!"),
            TestContext.Current.CancellationToken);
        Assert.True(acceptResult.IsSuccess);
        var owner2UserId = acceptResult.Value.UserId;

        testCurrentUser.SetUser(owner1UserId, owner1Email);
        var members = await listMembersHandler.HandleAsync(new ListTenantMembersQuery(tenantId), TestContext.Current.CancellationToken);
        var owner1MembershipId = members.Value.Single(m => m.UserId == owner1UserId).MembershipId;
        var owner2MembershipId = members.Value.Single(m => m.UserId == owner2UserId).MembershipId;

        // 2. Concurrently execute Request A (deactivate Owner 1) and Request B (deactivate Owner 2) using separate scopes
        // Both scopes resolve from the same service provider -> same database -> real concurrent SQL locking.
        using var scopeA = services.CreateScope();
        var handlerA = scopeA.ServiceProvider.GetRequiredService<UpdateTenantMembershipStatusHandler>();
        var tenantContextA = scopeA.ServiceProvider.GetRequiredService<ITenantContext>();
        var currentUserA = (TestCurrentUser)scopeA.ServiceProvider.GetRequiredService<ICurrentUser>();
        tenantContextA.Establish(tenantId);
        currentUserA.SetUser(owner1UserId, owner1Email);

        using var scopeB = services.CreateScope();
        var handlerB = scopeB.ServiceProvider.GetRequiredService<UpdateTenantMembershipStatusHandler>();
        var tenantContextB = scopeB.ServiceProvider.GetRequiredService<ITenantContext>();
        var currentUserB = (TestCurrentUser)scopeB.ServiceProvider.GetRequiredService<ICurrentUser>();
        tenantContextB.Establish(tenantId);
        currentUserB.SetUser(owner2UserId, owner2Email);

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var taskA = Task.Run(async () =>
        {
            await barrier.Task;
            return await handlerA.HandleAsync(
                new UpdateTenantMembershipStatusCommand(tenantId, owner1MembershipId, TenantMembershipStatus.Suspended),
                TestContext.Current.CancellationToken);
        });

        var taskB = Task.Run(async () =>
        {
            await barrier.Task;
            return await handlerB.HandleAsync(
                new UpdateTenantMembershipStatusCommand(tenantId, owner2MembershipId, TenantMembershipStatus.Suspended),
                TestContext.Current.CancellationToken);
        });

        // Trigger both tasks concurrently
        barrier.SetResult();
        var results = await Task.WhenAll(taskA, taskB);

        var resultA = results[0];
        var resultB = results[1];

        // 3. Verify exactly one succeeded and one failed
        var successCount = (resultA.IsSuccess ? 1 : 0) + (resultB.IsSuccess ? 1 : 0);
        var failureCount = (resultA.IsFailure ? 1 : 0) + (resultB.IsFailure ? 1 : 0);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // 4. Verify that the final active Owner count in the database is exactly 1 (never 0)
        using var verifyScope = services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var finalActiveOwners = await verifyDbContext.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId &&
                        m.Status == TenantMembershipStatus.Active &&
                        (m.UserId == owner1UserId || m.UserId == owner2UserId))
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, finalActiveOwners);
    }

    private static IServiceProvider CreateServiceProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Database:ConnectionString", connectionString),
                new KeyValuePair<string, string?>("Email:Provider", "Brevo"),
                new KeyValuePair<string, string?>("Email:FromName", "TeacherOS"),
                new KeyValuePair<string, string?>("Email:FromAddress", "noreply@teachos.local"),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddInfrastructure(configuration);

        // Override ICurrentUser with mutable test double
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        // Replace email sender with memory test double
        services.AddSingleton<TestEmailSender>();
        services.AddSingleton<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>(sp =>
            sp.GetRequiredService<TestEmailSender>());

        services.AddScoped<RegisterHandler>();
        services.AddScoped<CreateTenantInvitationHandler>();
        services.AddScoped<AcceptTenantInvitationHandler>();
        services.AddScoped<ListTenantMembersHandler>();
        services.AddScoped<UpdateTenantMembershipStatusHandler>();

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

    private sealed class TestEmailSender : TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender
    {
        public List<TeacherOS.Application.Abstractions.Email.InvitationEmailRequest> SentRequests { get; } = [];

        public Task<TeacherOS.Application.Abstractions.Email.EmailDispatchResult> SendInvitationEmailAsync(
            TeacherOS.Application.Abstractions.Email.InvitationEmailRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (SentRequests)
            {
                SentRequests.Add(request);
            }
            return Task.FromResult(TeacherOS.Application.Abstractions.Email.EmailDispatchResult.Success("test-msg-id"));
        }
    }
}
