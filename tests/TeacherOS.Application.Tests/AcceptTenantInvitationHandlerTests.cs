using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Common;
using TeacherOS.Application.Invitations;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class AcceptTenantInvitationHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeTenantInvitationStore _invitationStore = new();
    private readonly FakeTenantMembershipManagementStore _membershipStore = new();
    private readonly FakeIdentityUserRegistrar _userRegistrar = new();
    private readonly FakeInvitationTokenService _tokenService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TestTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Anonymous_user_can_accept_invitation_with_password()
    {
        var invitation = CreateInvitation("newuser@school.local");
        _invitationStore.Invitation = invitation;

        var handler = new AcceptTenantInvitationHandler(
            new FakeCurrentUser(null),
            _invitationStore,
            _membershipStore,
            _userRegistrar,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new AcceptTenantInvitationCommand("raw-token", "StrongPassword123!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsNewUser);
        Assert.Equal("newuser@school.local", result.Value.Email);
        Assert.True(invitation.IsAccepted);
        Assert.NotNull(_membershipStore.AddedMembership);
        Assert.Equal(_tenantId, _membershipStore.AddedMembership.TenantId);
        Assert.Equal(TenantMembershipStatus.Active, _membershipStore.AddedMembership.Status);
    }

    [Fact]
    public async Task Existing_authenticated_user_with_matching_email_can_accept_invitation()
    {
        var existingUserId = Guid.NewGuid();
        var invitation = CreateInvitation("existing@school.local");
        _invitationStore.Invitation = invitation;
        _membershipStore.UserEmail = "existing@school.local";

        var handler = new AcceptTenantInvitationHandler(
            new FakeCurrentUser(existingUserId),
            _invitationStore,
            _membershipStore,
            _userRegistrar,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new AcceptTenantInvitationCommand("raw-token"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsNewUser);
        Assert.Equal(existingUserId, result.Value.UserId);
        Assert.True(invitation.IsAccepted);
        Assert.NotNull(_membershipStore.AddedMembership);
        Assert.Equal(existingUserId, _membershipStore.AddedMembership.UserId);
    }

    [Fact]
    public async Task Existing_authenticated_user_with_mismatched_email_is_rejected()
    {
        var existingUserId = Guid.NewGuid();
        var invitation = CreateInvitation("invited@school.local");
        _invitationStore.Invitation = invitation;
        _membershipStore.UserEmail = "other@school.local";

        var handler = new AcceptTenantInvitationHandler(
            new FakeCurrentUser(existingUserId),
            _invitationStore,
            _membershipStore,
            _userRegistrar,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new AcceptTenantInvitationCommand("raw-token"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.EmailMismatch, result.Error);
        Assert.False(invitation.IsAccepted);
    }

    [Fact]
    public async Task Expired_invitation_cannot_be_accepted()
    {
        var invitation = CreateInvitation("newuser@school.local", isExpired: true);
        _invitationStore.Invitation = invitation;

        var handler = new AcceptTenantInvitationHandler(
            new FakeCurrentUser(null),
            _invitationStore,
            _membershipStore,
            _userRegistrar,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new AcceptTenantInvitationCommand("raw-token", "StrongPassword123!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.Expired, result.Error);
    }

    [Fact]
    public async Task Revoked_invitation_cannot_be_accepted()
    {
        var invitation = CreateInvitation("newuser@school.local");
        invitation.Revoke(_timeProvider.GetUtcNow());
        _invitationStore.Invitation = invitation;

        var handler = new AcceptTenantInvitationHandler(
            new FakeCurrentUser(null),
            _invitationStore,
            _membershipStore,
            _userRegistrar,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new AcceptTenantInvitationCommand("raw-token", "StrongPassword123!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.Revoked, result.Error);
    }

    private TenantInvitation CreateInvitation(string email, bool isExpired = false)
    {
        var now = _timeProvider.GetUtcNow();
        var createdAt = isExpired ? now.AddDays(-10) : now;
        var expiresAt = isExpired ? now.AddDays(-3) : now.AddDays(7);

        return new TenantInvitation(
            Guid.NewGuid(),
            _tenantId,
            email,
            email.ToUpperInvariant(),
            "hash-raw-token",
            Guid.NewGuid(),
            createdAt,
            expiresAt);
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
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
        public string? UserEmail { get; set; }
        public TenantMembership? AddedMembership { get; private set; }

        public Task<IReadOnlyList<TenantMemberListItem>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantMemberListItem>>(Array.Empty<TenantMemberListItem>());

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
            Task.FromResult(UserEmail);

        public void AddMembership(TenantMembership membership)
        {
            AddedMembership = membership;
        }
    }

    private sealed class FakeIdentityUserRegistrar : IIdentityUserRegistrar
    {
        public Task<Result<IdentityRegistrationResult>> RegisterAsync(string email, string password, CancellationToken cancellationToken) =>
            Task.FromResult(Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), email)));
    }

    private sealed class FakeInvitationTokenService : IInvitationTokenService
    {
        public string GenerateRawToken() => "raw-token";
        public string HashToken(string rawToken) => "hash-" + rawToken;
        public string ProtectToken(string rawToken) => "protected-" + rawToken;
        public string UnprotectToken(string protectedToken) => protectedToken.Replace("protected-", "");
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
