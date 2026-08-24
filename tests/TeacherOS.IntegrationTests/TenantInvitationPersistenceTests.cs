using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Invitations;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Email;
using TeacherOS.Infrastructure.Persistence;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class TenantInvitationPersistenceTests
{
    private const string DefaultConnectionString =
        "Server=localhost\\MSSQLSERVER01;Database=TeacherOS;Trusted_Connection=true;TrustServerCertificate=true;Encrypt=true;";

    private static readonly object DatabaseInitLock = new();
    private static bool _databaseInitialized;

    [Fact]
    public async Task Real_invitation_lifecycle_and_security_invariants_persisted_correctly()
    {
        var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var registerHandler = sp.GetRequiredService<RegisterHandler>();
        var inviteHandler = sp.GetRequiredService<CreateTenantInvitationHandler>();
        var inspectHandler = sp.GetRequiredService<InspectTenantInvitationHandler>();
        var revokeHandler = sp.GetRequiredService<RevokeTenantInvitationHandler>();
        var listInvitationsHandler = sp.GetRequiredService<ListTenantInvitationsHandler>();
        var acceptHandler = sp.GetRequiredService<AcceptTenantInvitationHandler>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var testCurrentUser = (TestCurrentUser)sp.GetRequiredService<ICurrentUser>();
        var emailSender = (TestEmailSender)sp.GetRequiredService<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"inv_owner_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"Invitation Academy {testSuffix}";

        // 1. Create Tenant
        var regResult = await registerHandler.HandleAsync(
            new RegisterCommand(ownerEmail, password, tenantName),
            TestContext.Current.CancellationToken);
        Assert.True(regResult.IsSuccess);
        var tenantId = regResult.Value.TenantId;
        var ownerUserId = regResult.Value.UserId;

        tenantContext.Establish(tenantId);
        testCurrentUser.SetUser(ownerUserId, ownerEmail);

        // 2. Create Invitation
        var invitedEmail = $"invited_{testSuffix}@test-academy.local";
        var inviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, invitedEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess);
        var invitationId = inviteResult.Value.InvitationId;

        // Verify Database Security:
        // TenantInvitations table contains TokenHash, NEVER raw token
        var persistedInvitation = await dbContext.TenantInvitations.AsNoTracking().SingleAsync(i => i.Id == invitationId, TestContext.Current.CancellationToken);
        Assert.NotNull(persistedInvitation.TokenHash);
        Assert.DoesNotContain("raw", persistedInvitation.TokenHash);
        Assert.Null(persistedInvitation.AcceptedAtUtc);
        Assert.Null(persistedInvitation.RevokedAtUtc);

        // EmailOutboxMessages table contains ProtectedInvitationToken, NEVER plaintext token
        var persistedOutbox = await dbContext.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.TenantInvitationId == invitationId, TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Sent, persistedOutbox.Status);
        // After sending, protected token is cleared
        Assert.Null(persistedOutbox.ProtectedInvitationToken);

        // Retrieve raw token from test email sender
        var capturedEmail = emailSender.SentRequests.Last(r => r.RecipientEmail == invitedEmail);
        var rawToken = capturedEmail.RawInvitationToken;

        // 3. Inspect invitation anonymously
        testCurrentUser.Clear();
        var inspectResult = await inspectHandler.HandleAsync(
            new InspectTenantInvitationQuery(rawToken),
            TestContext.Current.CancellationToken);
        Assert.True(inspectResult.IsSuccess);
        Assert.Equal(tenantName, inspectResult.Value.TenantName);
        Assert.Equal(invitedEmail, inspectResult.Value.Email);
        Assert.Equal("Pending", inspectResult.Value.Status);

        // 4. Duplicate pending invitation rejected
        testCurrentUser.SetUser(ownerUserId, ownerEmail);
        var duplicateInviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, invitedEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(duplicateInviteResult.IsFailure);
        Assert.Equal(InvitationErrors.PendingInvitationExists, duplicateInviteResult.Error);

        // 5. Accept invitation as new user
        testCurrentUser.Clear();
        var acceptResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(rawToken, "NewUserPassword@2026!"),
            TestContext.Current.CancellationToken);
        Assert.True(acceptResult.IsSuccess);
        Assert.True(acceptResult.Value.IsNewUser);
        var newUserId = acceptResult.Value.UserId;

        // 6. Verify invitation is marked Accepted in DB and cannot be reused
        var acceptedInviteInDb = await dbContext.TenantInvitations.AsNoTracking().SingleAsync(i => i.Id == invitationId, TestContext.Current.CancellationToken);
        Assert.NotNull(acceptedInviteInDb.AcceptedAtUtc);
        Assert.True(acceptedInviteInDb.IsAccepted);

        var reuseResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(rawToken, "AnotherPassword@2026!"),
            TestContext.Current.CancellationToken);
        Assert.True(reuseResult.IsFailure);
        Assert.Equal(InvitationErrors.AlreadyAccepted, reuseResult.Error);

        // 7. Cannot invite already active member
        testCurrentUser.SetUser(ownerUserId, ownerEmail);
        var inviteActiveMemberResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, invitedEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(inviteActiveMemberResult.IsFailure);
        Assert.Equal(InvitationErrors.MemberAlreadyExists, inviteActiveMemberResult.Error);

        // 8. Test Revoke flow
        var secondInvitedEmail = $"second_invited_{testSuffix}@test-academy.local";
        var secondInviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, secondInvitedEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(secondInviteResult.IsSuccess);
        var secondInvitationId = secondInviteResult.Value.InvitationId;
        var secondRawToken = emailSender.SentRequests.Last(r => r.RecipientEmail == secondInvitedEmail).RawInvitationToken;

        var revokeResult = await revokeHandler.HandleAsync(
            new RevokeTenantInvitationCommand(tenantId, secondInvitationId),
            TestContext.Current.CancellationToken);
        Assert.True(revokeResult.IsSuccess);

        testCurrentUser.Clear();
        var acceptRevokedResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(secondRawToken, "Password@2026!"),
            TestContext.Current.CancellationToken);
        Assert.True(acceptRevokedResult.IsFailure);
        Assert.Equal(InvitationErrors.Revoked, acceptRevokedResult.Error);
    }

    [Fact]
    public async Task Existing_user_can_accept_invitation_to_second_tenant()
    {
        var services = CreateServiceProvider();
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var registerHandler = sp.GetRequiredService<RegisterHandler>();
        var inviteHandler = sp.GetRequiredService<CreateTenantInvitationHandler>();
        var acceptHandler = sp.GetRequiredService<AcceptTenantInvitationHandler>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var testCurrentUser = (TestCurrentUser)sp.GetRequiredService<ICurrentUser>();
        var emailSender = (TestEmailSender)sp.GetRequiredService<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var teacherEmail = $"teacher_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";

        // Register School 1 (teacher is owner of School 1)
        var regSchool1 = await registerHandler.HandleAsync(
            new RegisterCommand(teacherEmail, password, $"First School {testSuffix}"),
            TestContext.Current.CancellationToken);
        Assert.True(regSchool1.IsSuccess);
        var teacherUserId = regSchool1.Value.UserId;

        // Register School 2 (by different owner)
        var school2OwnerEmail = $"owner2_{testSuffix}@test-academy.local";
        var regSchool2 = await registerHandler.HandleAsync(
            new RegisterCommand(school2OwnerEmail, password, $"Second School {testSuffix}"),
            TestContext.Current.CancellationToken);
        Assert.True(regSchool2.IsSuccess);
        var school2TenantId = regSchool2.Value.TenantId;
        var school2OwnerUserId = regSchool2.Value.UserId;

        // School 2 Owner invites teacherEmail
        tenantContext.Establish(school2TenantId);
        testCurrentUser.SetUser(school2OwnerUserId, school2OwnerEmail);

        var inviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(school2TenantId, teacherEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess);

        var rawToken = emailSender.SentRequests.Last(r => r.RecipientEmail == teacherEmail).RawInvitationToken;

        // Teacher accepts invitation while logged in as teacherEmail
        testCurrentUser.SetUser(teacherUserId, teacherEmail);
        var acceptResult = await acceptHandler.HandleAsync(
            new AcceptTenantInvitationCommand(rawToken),
            TestContext.Current.CancellationToken);
        Assert.True(acceptResult.IsSuccess);
        Assert.False(acceptResult.Value.IsNewUser);
        Assert.Equal(teacherUserId, acceptResult.Value.UserId);

        // Verify teacher now has active memberships in BOTH School 1 and School 2
        var memberships = await dbContext.TenantMemberships.AsNoTracking().Where(m => m.UserId == teacherUserId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, m => Assert.Equal(TenantMembershipStatus.Active, m.Status));
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var connectionString = ResolveConnectionString();

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

        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        services.AddSingleton<TestEmailSender>();
        services.AddSingleton<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>(sp =>
            sp.GetRequiredService<TestEmailSender>());

        services.AddScoped<RegisterHandler>();
        services.AddScoped<CreateTenantInvitationHandler>();
        services.AddScoped<InspectTenantInvitationHandler>();
        services.AddScoped<RevokeTenantInvitationHandler>();
        services.AddScoped<ListTenantInvitationsHandler>();
        services.AddScoped<AcceptTenantInvitationHandler>();

        var serviceProvider = services.BuildServiceProvider();
        EnsureDatabaseMigrated(serviceProvider);

        return serviceProvider;
    }

    private static string ResolveConnectionString()
    {
        var testEnvConn = Environment.GetEnvironmentVariable("TEACHEROS_TEST_DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(testEnvConn))
        {
            return testEnvConn;
        }

        var databaseEnvConn = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (!string.IsNullOrWhiteSpace(databaseEnvConn))
        {
            return databaseEnvConn;
        }

        return DefaultConnectionString;
    }

    private static void EnsureDatabaseMigrated(IServiceProvider serviceProvider)
    {
        if (_databaseInitialized)
        {
            return;
        }

        lock (DatabaseInitLock)
        {
            if (_databaseInitialized)
            {
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();
            _databaseInitialized = true;
        }
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
