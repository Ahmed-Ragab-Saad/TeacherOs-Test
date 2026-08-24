using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Common;
using TeacherOS.Application.Invitations;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Persistence;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class RealSqlTransactionRollbackIntegrationTests
{
    [Fact]
    public async Task Identity_user_creation_rolls_back_if_invitation_acceptance_fails_on_save()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var registerHandler = sp.GetRequiredService<RegisterHandler>();
        var inviteHandler = sp.GetRequiredService<CreateTenantInvitationHandler>();
        var userRegistrar = sp.GetRequiredService<IIdentityUserRegistrar>();
        var invitationStore = sp.GetRequiredService<ITenantInvitationStore>();
        var membershipStore = sp.GetRequiredService<ITenantMembershipManagementStore>();
        var tokenService = sp.GetRequiredService<IInvitationTokenService>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var testCurrentUser = (TestCurrentUser)sp.GetRequiredService<ICurrentUser>();
        var emailSender = (TestEmailSender)sp.GetRequiredService<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner_{testSuffix}@test-academy.local";
        var password = "StrongPassword@2026!";
        var tenantName = $"Rollback Academy {testSuffix}";

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
        var invitedEmail = $"new_user_{testSuffix}@test-academy.local";
        var inviteResult = await inviteHandler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, invitedEmail, null),
            TestContext.Current.CancellationToken);
        Assert.True(inviteResult.IsSuccess);
        var invitationId = inviteResult.Value.InvitationId;

        var rawToken = emailSender.SentRequests.Last(r => r.RecipientEmail == invitedEmail).RawInvitationToken;

        // 3. Use a FailingUnitOfWork that throws on SaveChangesAsync
        var failingUnitOfWork = new FailingUnitOfWork(unitOfWork);
        var acceptHandler = new AcceptTenantInvitationHandler(
            new TestCurrentUser(),
            invitationStore,
            membershipStore,
            userRegistrar,
            tokenService,
            failingUnitOfWork,
            TimeProvider.System);

        // 4. Attempt acceptance which will fail during SaveChangesAsync inside the transaction
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            acceptHandler.HandleAsync(
                new AcceptTenantInvitationCommand(rawToken, "SecretPassword@2026!"),
                TestContext.Current.CancellationToken));

        // 5. Verify that in real SQL Server:
        // - Identity user was NOT persisted (rolled back)
        var createdUser = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == invitedEmail, TestContext.Current.CancellationToken);
        Assert.Null(createdUser);

        // - TenantMembership was NOT created
        var memberships = await dbContext.TenantMemberships.AsNoTracking().Where(m => m.TenantId == tenantId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(memberships); // Only owner remains
        Assert.Equal(ownerUserId, memberships[0].UserId);

        // - TenantInvitation is NOT marked Accepted
        var invitationInDb = await dbContext.TenantInvitations.AsNoTracking().SingleAsync(i => i.Id == invitationId, TestContext.Current.CancellationToken);
        Assert.Null(invitationInDb.AcceptedAtUtc);
        Assert.False(invitationInDb.IsAccepted);
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

        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        services.AddSingleton<TestEmailSender>();
        services.AddSingleton<TeacherOS.Application.Abstractions.Email.ITransactionalEmailSender>(sp =>
            sp.GetRequiredService<TestEmailSender>());

        services.AddScoped<RegisterHandler>();
        services.AddScoped<CreateTenantInvitationHandler>();
        services.AddScoped<AcceptTenantInvitationHandler>();

        return services.BuildServiceProvider();
    }

    private sealed class FailingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated unexpected database failure during invitation acceptance.");
        }

        public Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default)
        {
            return inner.ExecuteInTransactionAsync(operation, cancellationToken);
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
