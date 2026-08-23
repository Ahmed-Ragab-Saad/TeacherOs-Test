using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using Xunit;

namespace TeacherOS.Application.Tests;

public sealed class RegisterHandlerTests
{
    [Fact]
    public async Task Successful_registration_creates_user_tenant_owner_role_membership_and_commits()
    {
        var userId = Guid.NewGuid();
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(userId, "owner@example.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();

        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand("  owner@example.com  ", "SecurePassword123!", "  North Academy  "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("owner@example.com", result.Value.Email);
        Assert.NotEqual(Guid.Empty, result.Value.TenantId);

        Assert.Equal("owner@example.com", registrar.PassedEmail);
        Assert.Equal("SecurePassword123!", registrar.PassedPassword);

        Assert.NotNull(onboardingStore.Tenant);
        Assert.Equal("North Academy", onboardingStore.Tenant.Name);
        Assert.Equal(TenantStatus.Active, onboardingStore.Tenant.Status);
        Assert.Equal(result.Value.TenantId, onboardingStore.Tenant.Id);

        Assert.NotNull(onboardingStore.Role);
        Assert.Equal("Owner", onboardingStore.Role.Name);
        Assert.Equal(result.Value.TenantId, onboardingStore.Role.TenantId);
        Assert.Equal(Permission.All, onboardingStore.Role.PermissionCodes);

        Assert.NotNull(onboardingStore.Membership);
        Assert.Equal(result.Value.TenantId, onboardingStore.Membership.TenantId);
        Assert.Equal(userId, onboardingStore.Membership.UserId);
        Assert.Equal(TenantMembershipStatus.Active, onboardingStore.Membership.Status);
        Assert.Equal(onboardingStore.Role.Id, onboardingStore.Membership.RoleId);

        Assert.Equal(1, unitOfWork.SaveChangesCount);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_email_returns_validation_error(string? email)
    {
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "a@b.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand(email, "Password123!", "Tenant"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidEmail, result.Error);
        Assert.Equal(0, registrar.CallCount);
        Assert.Equal(0, unitOfWork.ExecuteInTransactionCount);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@missinguser.com")]
    [InlineData("user@")]
    public async Task Invalid_email_format_returns_validation_error(string invalidEmail)
    {
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "a@b.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand(invalidEmail, "Password123!", "Tenant"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidEmail, result.Error);
        Assert.Equal(0, registrar.CallCount);
        Assert.Equal(0, unitOfWork.ExecuteInTransactionCount);
    }

    [Fact]
    public async Task Email_exceeding_max_length_returns_validation_error()
    {
        var tooLongEmail = new string('a', 250) + "@example.com";
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "a@b.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand(tooLongEmail, "Password123!", "Tenant"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidEmail, result.Error);
        Assert.Equal(0, registrar.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Missing_password_returns_validation_error(string? password)
    {
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "a@b.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand("owner@example.com", password, "Tenant"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.PasswordRequired, result.Error);
        Assert.Equal(0, registrar.CallCount);
        Assert.Equal(0, unitOfWork.ExecuteInTransactionCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_tenant_name_returns_validation_error(string? tenantName)
    {
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "a@b.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand("owner@example.com", "Password123!", tenantName),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.TenantNameRequired, result.Error);
        Assert.Equal(0, registrar.CallCount);
        Assert.Equal(0, unitOfWork.ExecuteInTransactionCount);
    }

    [Fact]
    public async Task Tenant_name_exceeding_max_length_returns_validation_error()
    {
        var tooLongName = new string('A', Tenant.MaxNameLength + 1);
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "a@b.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand("owner@example.com", "Password123!", tooLongName),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.TenantNameTooLong, result.Error);
        Assert.Equal(0, registrar.CallCount);
    }

    [Fact]
    public async Task Duplicate_email_returns_conflict_error_and_does_not_persist_tenant()
    {
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Failure(AuthenticationErrors.DuplicateEmail));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand("existing@example.com", "Password123!", "Tenant"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.DuplicateEmail, result.Error);
        Assert.Null(onboardingStore.Tenant);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCount);
    }

    [Fact]
    public async Task Identity_creation_failure_returns_error_and_does_not_persist_tenant()
    {
        var identityError = new Error("Authentication.InvalidPassword", "Password is too weak.", ErrorType.Validation);
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Failure(identityError));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        var result = await handler.HandleAsync(
            new RegisterCommand("owner@example.com", "weak", "Tenant"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(identityError, result.Error);
        Assert.Null(onboardingStore.Tenant);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Equal(1, unitOfWork.ExecuteInTransactionCount);
    }

    [Fact]
    public async Task Onboarding_persistence_failure_bubbles_exception()
    {
        var userId = Guid.NewGuid();
        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(userId, "owner@example.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork { ThrowOnSave = true };
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(
                new RegisterCommand("owner@example.com", "Password123!", "Tenant"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancellation_is_propagated_and_aborts_registration()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var registrar = new StubIdentityUserRegistrar(
            Result<IdentityRegistrationResult>.Success(new IdentityRegistrationResult(Guid.NewGuid(), "owner@example.com")));
        var onboardingStore = new StubTenantOnboardingStore();
        var unitOfWork = new StubUnitOfWork();
        var handler = new RegisterHandler(registrar, onboardingStore, unitOfWork);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(
                new RegisterCommand("owner@example.com", "Password123!", "Tenant"),
                cts.Token));

        Assert.Equal(0, registrar.CallCount);
        Assert.Equal(0, unitOfWork.ExecuteInTransactionCount);
    }

    private sealed class StubIdentityUserRegistrar(Result<IdentityRegistrationResult> result) : IIdentityUserRegistrar
    {
        internal int CallCount { get; private set; }
        internal string? PassedEmail { get; private set; }
        internal string? PassedPassword { get; private set; }

        public Task<Result<IdentityRegistrationResult>> RegisterAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            PassedEmail = email;
            PassedPassword = password;
            return Task.FromResult(result);
        }
    }

    private sealed class StubTenantOnboardingStore : ITenantOnboardingStore
    {
        internal Tenant? Tenant { get; private set; }
        internal Role? Role { get; private set; }
        internal TenantMembership? Membership { get; private set; }

        public void Add(Tenant tenant, Role ownerRole, TenantMembership membership)
        {
            Tenant = tenant;
            Role = ownerRole;
            Membership = membership;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        internal int SaveChangesCount { get; private set; }
        internal int ExecuteInTransactionCount { get; private set; }
        internal bool ThrowOnSave { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("Simulated database failure.");
            }

            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<Result<T>> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteInTransactionCount++;
            return operation(cancellationToken);
        }
    }
}
