using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Application.Invitations;
using TeacherOS.Application.Memberships;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class InvitationAndMemberQueriesTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TestTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Inspect_valid_invitation_returns_safe_metadata()
    {
        var invitation = new TenantInvitation(
            Guid.NewGuid(),
            _tenantId,
            "invited@school.local",
            "INVITED@SCHOOL.LOCAL",
            "hash-my-token",
            Guid.NewGuid(),
            _timeProvider.GetUtcNow(),
            _timeProvider.GetUtcNow().AddDays(7));

        var invitationStore = new FakeTenantInvitationStore { Invitation = invitation };
        var membershipStore = new FakeTenantMembershipManagementStore();
        var tokenService = new FakeTokenService();

        var handler = new InspectTenantInvitationHandler(
            invitationStore,
            membershipStore,
            tokenService,
            _timeProvider);

        var result = await handler.HandleAsync(
            new InspectTenantInvitationQuery("my-token"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("invited@school.local", result.Value.Email);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal("Test Tenant", result.Value.TenantName);
    }

    [Fact]
    public async Task Revoke_invitation_marks_it_revoked()
    {
        var invitation = new TenantInvitation(
            Guid.NewGuid(),
            _tenantId,
            "invited@school.local",
            "INVITED@SCHOOL.LOCAL",
            "hash-token",
            Guid.NewGuid(),
            _timeProvider.GetUtcNow(),
            _timeProvider.GetUtcNow().AddDays(7));

        var invitationStore = new FakeTenantInvitationStore { Invitation = invitation };
        var unitOfWork = new FakeUnitOfWork();

        var handler = new RevokeTenantInvitationHandler(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeTenantContext(_tenantId),
            invitationStore,
            unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new RevokeTenantInvitationCommand(_tenantId, invitation.Id),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(invitation.IsRevoked);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task List_members_returns_members_from_store()
    {
        var membershipStore = new FakeTenantMembershipManagementStore
        {
            Members =
            [
                new TenantMemberListItem(Guid.NewGuid(), Guid.NewGuid(), "owner@school.local", Guid.NewGuid(), "Owner", "Active"),
                new TenantMemberListItem(Guid.NewGuid(), Guid.NewGuid(), "teacher@school.local", null, null, "Active"),
            ]
        };

        var handler = new ListTenantMembersHandler(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeTenantContext(_tenantId),
            membershipStore);

        var result = await handler.HandleAsync(
            new ListTenantMembersQuery(_tenantId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("owner@school.local", result.Value[0].Email);
        Assert.Equal("Owner", result.Value[0].RoleName);
        Assert.Null(result.Value[1].RoleName);
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
    }

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public bool IsAvailable => true;
        public Guid TenantId => tenantId;
        public void Establish(Guid establishedTenantId) { }
    }

    private sealed class FakeTokenService : IInvitationTokenService
    {
        public string GenerateRawToken() => "token";
        public string HashToken(string rawToken) => "hash-" + rawToken;
        public string ProtectToken(string rawToken) => "protected-" + rawToken;
        public string UnprotectToken(string protectedToken) => protectedToken.Replace("protected-", "");
    }

    private sealed class FakeTenantInvitationStore : ITenantInvitationStore
    {
        public TenantInvitation? Invitation { get; set; }

        public Task<TenantInvitation?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Invitation);

        public Task<TenantInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Invitation);

        public Task<bool> HasPendingInvitationAsync(Guid tenantId, string normalizedEmail, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<TenantInvitationListItem>> ListByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantInvitationListItem>>(Array.Empty<TenantInvitationListItem>());

        public void Add(TenantInvitation invitation, Guid outboxMessageId, string recipientEmail, string protectedToken, DateTimeOffset createdAtUtc) { }
    }

    private sealed class FakeTenantMembershipManagementStore : ITenantMembershipManagementStore
    {
        public IReadOnlyList<TenantMemberListItem> Members { get; set; } = [];

        public Task<IReadOnlyList<TenantMemberListItem>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Members);

        public Task<TenantMembership?> GetMembershipAsync(Guid membershipId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<bool> HasActiveMembershipAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasActiveMembershipForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsRoleValidForTenantAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<bool> IsMemberActiveOwnerAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("Test Tenant");

        public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetUserEmailAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public void AddMembership(TenantMembership membership) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
