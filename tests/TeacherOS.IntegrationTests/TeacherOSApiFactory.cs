using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Identity;

namespace TeacherOS.IntegrationTests;

public sealed class TeacherOSApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] =
                    "Server=localhost;Database=TeacherOSIntegrationTests;Integrated Security=true;Encrypt=false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentityAuthenticator>();
            services.RemoveAll<ICurrentSessionReader>();
            services.RemoveAll<ITenantMembershipResolver>();
            services.RemoveAll<IIdentityPrincipalFactory>();
            services.RemoveAll<IIdentityUserRegistrar>();
            services.RemoveAll<ITenantOnboardingStore>();
            services.RemoveAll<IUnitOfWork>();
            services.RemoveAll<IXmlRepository>();

            services.AddSingleton<IIdentityAuthenticator, TestIdentityAuthenticator>();
            services.AddSingleton<ICurrentSessionReader, TestCurrentSessionReader>();
            services.AddSingleton<ITenantMembershipResolver, TestTenantMembershipResolver>();
            services.AddSingleton<IIdentityPrincipalFactory, TestIdentityPrincipalFactory>();
            services.AddSingleton<IIdentityUserRegistrar, TestIdentityUserRegistrar>();
            services.AddSingleton<ITenantOnboardingStore, TestTenantOnboardingStore>();
            services.AddSingleton<IUnitOfWork, TestUnitOfWork>();

            services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(options =>
            {
                options.XmlRepository = new EphemeralXmlRepository();
            });
        });
    }

    private sealed class EphemeralXmlRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_elements)
            {
                return _elements.ToList().AsReadOnly();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_elements)
            {
                _elements.Add(new XElement(element));
            }
        }
    }

    private sealed class TestIdentityUserRegistrar : IIdentityUserRegistrar
    {
        public Task<Result<IdentityRegistrationResult>> RegisterAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(email, TestAuthenticationData.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result<IdentityRegistrationResult>.Failure(AuthenticationErrors.DuplicateEmail));
            }

            return Task.FromResult(Result<IdentityRegistrationResult>.Success(
                new IdentityRegistrationResult(Guid.NewGuid(), email)));
        }
    }

    private sealed class TestTenantOnboardingStore : ITenantOnboardingStore
    {
        public void Add(Tenant tenant, Role ownerRole, TenantMembership membership)
        {
        }
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }

        public Task<Result<T>> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }

    private sealed class TestIdentityAuthenticator : IIdentityAuthenticator
    {
        public Task<IdentityAuthenticationResult?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IdentityAuthenticationResult? result =
                string.Equals(email, TestAuthenticationData.Email, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(password, TestAuthenticationData.Password, StringComparison.Ordinal)
                    ? new IdentityAuthenticationResult(TestAuthenticationData.UserId, TestAuthenticationData.Email)
                    : null;

            return Task.FromResult(result);
        }
    }

    private sealed class TestIdentityPrincipalFactory : IIdentityPrincipalFactory
    {
        public Task<ClaimsPrincipal?> CreateAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userId != TestAuthenticationData.UserId)
            {
                return Task.FromResult<ClaimsPrincipal?>(null);
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, TestAuthenticationData.Email),
                    new Claim(ClaimTypes.Name, TestAuthenticationData.Email),
                ],
                "TestAuthentication");

            return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
        }
    }

    private sealed class TestCurrentSessionReader : ICurrentSessionReader
    {
        public Task<CurrentSession?> GetAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CurrentSession? session = userId == TestAuthenticationData.UserId
                ? new CurrentSession(
                    TestAuthenticationData.UserId,
                    TestAuthenticationData.Email,
                    [
                        new CurrentTenantMembership(
                            TestAuthenticationData.FirstActiveTenantId,
                            "First School",
                            TenantStatus.Active,
                            TenantMembershipStatus.Active),
                        new CurrentTenantMembership(
                            TestAuthenticationData.SecondActiveTenantId,
                            "Second School",
                            TenantStatus.Trial,
                            TenantMembershipStatus.Active),
                        new CurrentTenantMembership(
                            TestAuthenticationData.SuspendedTenantId,
                            "Suspended School",
                            TenantStatus.Active,
                            TenantMembershipStatus.Suspended),
                    ])
                : null;

            return Task.FromResult(session);
        }
    }

    private sealed class TestTenantMembershipResolver : ITenantMembershipResolver
    {
        public Task<bool> HasActiveMembershipAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isActiveMember = userId == TestAuthenticationData.UserId &&
                (tenantId == TestAuthenticationData.FirstActiveTenantId ||
                 tenantId == TestAuthenticationData.SecondActiveTenantId);

            return Task.FromResult(isActiveMember);
        }
    }
}

internal static class TestAuthenticationData
{
    internal static readonly Guid UserId = Guid.NewGuid();
    internal static readonly Guid FirstActiveTenantId = Guid.NewGuid();
    internal static readonly Guid SecondActiveTenantId = Guid.NewGuid();
    internal static readonly Guid SuspendedTenantId = Guid.NewGuid();
    internal static readonly Guid NonMemberTenantId = Guid.NewGuid();
    internal const string Email = "teacher@example.com";
    internal const string Password = "Test-only-password-42!";
}
