using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Application.Memberships;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class UpdateTenantMembershipStatusHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeTenantMembershipManagementStore _membershipStore = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task Suspending_non_owner_member_succeeds()
    {
        var membershipId = Guid.NewGuid();
        var membership = new TenantMembership(membershipId, _tenantId, Guid.NewGuid(), TenantMembershipStatus.Active);
        _membershipStore.Membership = membership;
        _membershipStore.IsOwner = false;
        _membershipStore.ActiveOwnersCount = 1;

        var handler = new UpdateTenantMembershipStatusHandler(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeTenantContext(_tenantId),
            _membershipStore,
            _unitOfWork);

        var result = await handler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(_tenantId, membershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantMembershipStatus.Suspended, membership.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Suspending_the_last_active_owner_is_rejected()
    {
        var membershipId = Guid.NewGuid();
        var membership = new TenantMembership(membershipId, _tenantId, Guid.NewGuid(), TenantMembershipStatus.Active, Guid.NewGuid());
        _membershipStore.Membership = membership;
        _membershipStore.IsOwner = true;
        _membershipStore.ActiveOwnersCount = 1;

        var handler = new UpdateTenantMembershipStatusHandler(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeTenantContext(_tenantId),
            _membershipStore,
            _unitOfWork);

        var result = await handler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(_tenantId, membershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(MembershipErrors.CannotDisableLastOwner, result.Error);
        Assert.Equal(TenantMembershipStatus.Active, membership.Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Suspending_one_of_multiple_active_owners_succeeds()
    {
        var membershipId = Guid.NewGuid();
        var membership = new TenantMembership(membershipId, _tenantId, Guid.NewGuid(), TenantMembershipStatus.Active, Guid.NewGuid());
        _membershipStore.Membership = membership;
        _membershipStore.IsOwner = true;
        _membershipStore.ActiveOwnersCount = 2;

        var handler = new UpdateTenantMembershipStatusHandler(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeTenantContext(_tenantId),
            _membershipStore,
            _unitOfWork);

        var result = await handler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(_tenantId, membershipId, TenantMembershipStatus.Suspended),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantMembershipStatus.Suspended, membership.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Reactivating_suspended_member_succeeds()
    {
        var membershipId = Guid.NewGuid();
        var membership = new TenantMembership(membershipId, _tenantId, Guid.NewGuid(), TenantMembershipStatus.Suspended);
        _membershipStore.Membership = membership;

        var handler = new UpdateTenantMembershipStatusHandler(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeTenantContext(_tenantId),
            _membershipStore,
            _unitOfWork);

        var result = await handler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(_tenantId, membershipId, TenantMembershipStatus.Active),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantMembershipStatus.Active, membership.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
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

    private sealed class FakeTenantMembershipManagementStore : ITenantMembershipManagementStore
    {
        public TenantMembership? Membership { get; set; }
        public bool IsOwner { get; set; }
        public int ActiveOwnersCount { get; set; } = 1;

        public Task<IReadOnlyList<TenantMemberListItem>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantMemberListItem>>(Array.Empty<TenantMemberListItem>());

        public Task<TenantMembership?> GetMembershipAsync(Guid membershipId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Membership);

        public Task<bool> HasActiveMembershipAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasActiveMembershipForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsRoleValidForTenantAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveOwnersCount);

        public Task<bool> IsMemberActiveOwnerAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken = default) =>
            Task.FromResult(IsOwner);

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
