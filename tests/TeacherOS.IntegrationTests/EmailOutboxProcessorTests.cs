using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure;
using TeacherOS.Infrastructure.Email;
using TeacherOS.Infrastructure.Identity;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class EmailOutboxProcessorTests
{
    [Fact]
    public async Task Immediate_dispatch_success_marks_outbox_sent_and_clears_protected_token()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();
        var emailSender = (FakeTransactionalEmailSender)scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
        var processor = scope.ServiceProvider.GetRequiredService<IEmailOutboxProcessor>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"creator1_{suffix}@school.local", NormalizedUserName = $"CREATOR1_{suffix}@SCHOOL.LOCAL", Email = $"creator1_{suffix}@school.local", NormalizedEmail = $"CREATOR1_{suffix}@SCHOOL.LOCAL" };
        var tenant = new Tenant(Guid.NewGuid(), "Test School", TenantStatus.Active);
        var rawToken = tokenService.GenerateRawToken();
        var tokenHash = tokenService.HashToken(rawToken);
        var protectedToken = tokenService.ProtectToken(rawToken);
        var now = DateTimeOffset.UtcNow;

        var invitation = new TenantInvitation(
            Guid.NewGuid(),
            tenant.Id,
            $"test_{suffix}@school.local",
            $"TEST_{suffix}@SCHOOL.LOCAL",
            tokenHash,
            user.Id,
            now,
            now.AddDays(7));

        var outboxMessage = new EmailOutboxMessage(
            Guid.NewGuid(),
            invitation.Id,
            $"test_{suffix}@school.local",
            protectedToken,
            now);

        dbContext.Users.Add(user);
        dbContext.Tenants.Add(tenant);
        dbContext.TenantInvitations.Add(invitation);
        dbContext.EmailOutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        emailSender.ResultToReturn = EmailDispatchResult.Success("msg-123");

        var success = await processor.TryDispatchImmediatelyAsync(outboxMessage.Id, rawToken, TestContext.Current.CancellationToken);

        Assert.True(success);

        var reloaded = await dbContext.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == outboxMessage.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Sent, reloaded.Status);
        Assert.NotNull(reloaded.SentAtUtc);
        Assert.Equal("msg-123", reloaded.ProviderMessageId);
        Assert.Null(reloaded.ProtectedInvitationToken); // Cleared on success
        Assert.Single(emailSender.SentRequests);
        Assert.Equal($"test_{suffix}@school.local", emailSender.SentRequests[0].RecipientEmail);
    }

    [Fact]
    public async Task Transient_failure_schedules_retry_with_backoff_and_leaves_message_pending()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();
        var emailSender = (FakeTransactionalEmailSender)scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
        var processor = scope.ServiceProvider.GetRequiredService<IEmailOutboxProcessor>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"creator2_{suffix}@school.local", NormalizedUserName = $"CREATOR2_{suffix}@SCHOOL.LOCAL", Email = $"creator2_{suffix}@school.local", NormalizedEmail = $"CREATOR2_{suffix}@SCHOOL.LOCAL" };
        var tenant = new Tenant(Guid.NewGuid(), "Test School 2", TenantStatus.Active);
        var rawToken = tokenService.GenerateRawToken();
        var tokenHash = tokenService.HashToken(rawToken);
        var protectedToken = tokenService.ProtectToken(rawToken);
        var now = DateTimeOffset.UtcNow;

        var invitation = new TenantInvitation(
            Guid.NewGuid(),
            tenant.Id,
            $"test2_{suffix}@school.local",
            $"TEST2_{suffix}@SCHOOL.LOCAL",
            tokenHash,
            user.Id,
            now,
            now.AddDays(7));

        var outboxMessage = new EmailOutboxMessage(
            Guid.NewGuid(),
            invitation.Id,
            $"test2_{suffix}@school.local",
            protectedToken,
            now);

        dbContext.Users.Add(user);
        dbContext.Tenants.Add(tenant);
        dbContext.TenantInvitations.Add(invitation);
        dbContext.EmailOutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        emailSender.ResultToReturn = EmailDispatchResult.TransientFailure("RateLimit", "Rate limited", TimeSpan.FromSeconds(30));

        var success = await processor.TryDispatchImmediatelyAsync(outboxMessage.Id, rawToken, TestContext.Current.CancellationToken);

        Assert.False(success);

        var reloaded = await dbContext.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == outboxMessage.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Pending, reloaded.Status);
        Assert.Equal(1, reloaded.AttemptCount);
        Assert.NotNull(reloaded.LastAttemptAtUtc);
        Assert.True(reloaded.NextAttemptAtUtc > now);
        Assert.Equal("RateLimit", reloaded.LastErrorCode);
    }

    [Fact]
    public async Task Revoked_invitation_stops_email_delivery_and_marks_outbox_failed()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var services = CreateServiceProvider(db.ConnectionString);
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();
        var processor = scope.ServiceProvider.GetRequiredService<IEmailOutboxProcessor>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"creator3_{suffix}@school.local", NormalizedUserName = $"CREATOR3_{suffix}@SCHOOL.LOCAL", Email = $"creator3_{suffix}@school.local", NormalizedEmail = $"CREATOR3_{suffix}@SCHOOL.LOCAL" };
        var tenant = new Tenant(Guid.NewGuid(), "Test School 3", TenantStatus.Active);
        var rawToken = tokenService.GenerateRawToken();
        var tokenHash = tokenService.HashToken(rawToken);
        var protectedToken = tokenService.ProtectToken(rawToken);
        var now = DateTimeOffset.UtcNow;

        var invitation = new TenantInvitation(
            Guid.NewGuid(),
            tenant.Id,
            $"revoked_{suffix}@school.local",
            $"REVOKED_{suffix}@SCHOOL.LOCAL",
            tokenHash,
            user.Id,
            now,
            now.AddDays(7));

        invitation.Revoke(now);

        var outboxMessage = new EmailOutboxMessage(
            Guid.NewGuid(),
            invitation.Id,
            $"revoked_{suffix}@school.local",
            protectedToken,
            now);

        dbContext.Users.Add(user);
        dbContext.Tenants.Add(tenant);
        dbContext.TenantInvitations.Add(invitation);
        dbContext.EmailOutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var emailSender = (FakeTransactionalEmailSender)scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
        await processor.ProcessPendingOutboxMessagesAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(emailSender.SentRequests, r => r.RecipientEmail == $"revoked_{suffix}@school.local");

        var reloaded = await dbContext.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == outboxMessage.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Failed, reloaded.Status);
        Assert.Equal("InvitationInvalid", reloaded.LastErrorCode);
    }

    private static IServiceProvider CreateServiceProvider(string connectionString)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Database:ConnectionString", connectionString),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().SetApplicationName("TeacherOS");
        services.AddSingleton<ITenantContext, TenantContext>();

        services.AddInfrastructure(configuration);

        services.AddSingleton<FakeTransactionalEmailSender>();
        services.AddSingleton<ITransactionalEmailSender>(sp => sp.GetRequiredService<FakeTransactionalEmailSender>());

        return services.BuildServiceProvider();
    }

    private sealed class FakeTransactionalEmailSender : ITransactionalEmailSender
    {
        public List<InvitationEmailRequest> SentRequests { get; } = [];
        public EmailDispatchResult ResultToReturn { get; set; } = EmailDispatchResult.Success("default-id");

        public Task<EmailDispatchResult> SendInvitationEmailAsync(InvitationEmailRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            return Task.FromResult(ResultToReturn);
        }
    }
}
