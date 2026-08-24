using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Application.Invitations;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class CreateTenantInvitationHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeTenantContext _tenantContext;
    private readonly FakeTenantInvitationStore _invitationStore = new();
    private readonly FakeTenantMembershipManagementStore _membershipStore = new();
    private readonly FakeInvitationTokenService _tokenService = new();
    private readonly FakeEmailOutboxProcessor _emailProcessor = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TestTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    public CreateTenantInvitationHandlerTests()
    {
        _currentUser = new FakeCurrentUser(_userId);
        _tenantContext = new FakeTenantContext(_tenantId);
    }

    [Fact]
    public async Task Create_invitation_succeeds_and_attempts_immediate_dispatch()
    {
        var handler = new CreateTenantInvitationHandler(
            _currentUser,
            _tenantContext,
            _invitationStore,
            _membershipStore,
            _tokenService,
            _emailProcessor,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(_tenantId, "newmember@school.local", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sent", result.Value.DeliveryStatus);
        Assert.NotNull(_invitationStore.AddedInvitation);
        Assert.Equal("newmember@school.local", _invitationStore.AddedInvitation.Email);
        Assert.Equal("NEWMEMBER@SCHOOL.LOCAL", _invitationStore.AddedInvitation.NormalizedEmail);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Create_invitation_fails_if_email_is_invalid()
    {
        var handler = new CreateTenantInvitationHandler(
            _currentUser,
            _tenantContext,
            _invitationStore,
            _membershipStore,
            _tokenService,
            _emailProcessor,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(_tenantId, "not-an-email", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.InvalidEmail, result.Error);
    }

    [Fact]
    public async Task Create_invitation_fails_if_active_member_already_exists()
    {
        _membershipStore.ActiveMembersByEmail.Add("EXISTING@SCHOOL.LOCAL");

        var handler = new CreateTenantInvitationHandler(
            _currentUser,
            _tenantContext,
            _invitationStore,
            _membershipStore,
            _tokenService,
            _emailProcessor,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(_tenantId, "existing@school.local", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.MemberAlreadyExists, result.Error);
    }

    [Fact]
    public async Task Create_invitation_fails_if_pending_invitation_already_exists()
    {
        _invitationStore.PendingEmails.Add("PENDING@SCHOOL.LOCAL");

        var handler = new CreateTenantInvitationHandler(
            _currentUser,
            _tenantContext,
            _invitationStore,
            _membershipStore,
            _tokenService,
            _emailProcessor,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(_tenantId, "pending@school.local", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.PendingInvitationExists, result.Error);
    }

    [Fact]
    public async Task Create_invitation_fails_if_role_is_invalid_for_tenant()
    {
        var invalidRoleId = Guid.NewGuid();

        var handler = new CreateTenantInvitationHandler(
            _currentUser,
            _tenantContext,
            _invitationStore,
            _membershipStore,
            _tokenService,
            _emailProcessor,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(_tenantId, "member@school.local", invalidRoleId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(InvitationErrors.InvalidRole, result.Error);
    }

    [Fact]
    public async Task Create_invitation_falls_back_to_pending_if_immediate_dispatch_fails()
    {
        _emailProcessor.DispatchSuccess = false;

        var handler = new CreateTenantInvitationHandler(
            _currentUser,
            _tenantContext,
            _invitationStore,
            _membershipStore,
            _tokenService,
            _emailProcessor,
            _unitOfWork,
            _timeProvider);

        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(_tenantId, "member@school.local", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.DeliveryStatus);
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

    private sealed class FakeTenantInvitationStore : ITenantInvitationStore
    {
        public HashSet<string> PendingEmails { get; } = new(StringComparer.OrdinalIgnoreCase);
        public TenantInvitation? AddedInvitation { get; private set; }

        public Task<TenantInvitation?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<TenantInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<bool> HasPendingInvitationAsync(Guid tenantId, string normalizedEmail, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
            Task.FromResult(PendingEmails.Contains(normalizedEmail));

        public Task<IReadOnlyList<TenantInvitationListItem>> ListByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantInvitationListItem>>(Array.Empty<TenantInvitationListItem>());

        public void Add(TenantInvitation invitation, Guid outboxMessageId, string recipientEmail, string protectedToken, DateTimeOffset createdAtUtc)
        {
            AddedInvitation = invitation;
        }
    }

    private sealed class FakeTenantMembershipManagementStore : ITenantMembershipManagementStore
    {
        public HashSet<string> ActiveMembersByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<Guid> ValidRoles { get; } = [];

        public Task<IReadOnlyList<TenantMemberListItem>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantMemberListItem>>(Array.Empty<TenantMemberListItem>());

        public Task<TenantMembership?> GetMembershipAsync(Guid membershipId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<bool> HasActiveMembershipAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveMembersByEmail.Contains(normalizedEmail));

        public Task<bool> HasActiveMembershipForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsRoleValidForTenantAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ValidRoles.Contains(roleId));

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

    private sealed class FakeInvitationTokenService : IInvitationTokenService
    {
        public string GenerateRawToken() => "sample-raw-token";
        public string HashToken(string rawToken) => "hash-" + rawToken;
        public string ProtectToken(string rawToken) => "protected-" + rawToken;
        public string UnprotectToken(string protectedToken) => protectedToken.Replace("protected-", "");
    }

    private sealed class FakeEmailOutboxProcessor : IEmailOutboxProcessor
    {
        public bool DispatchSuccess { get; set; } = true;

        public Task<bool> TryDispatchImmediatelyAsync(Guid outboxMessageId, string rawToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(DispatchSuccess);

        public Task<int> ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
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
