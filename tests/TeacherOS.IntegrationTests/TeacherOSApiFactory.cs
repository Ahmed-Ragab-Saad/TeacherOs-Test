using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
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

            services.AddSingleton<IIdentityAuthenticator, TestIdentityAuthenticator>();
            services.AddSingleton<ICurrentSessionReader, TestCurrentSessionReader>();
            services.AddSingleton<ITenantMembershipResolver, TestTenantMembershipResolver>();
            services.AddSingleton<IIdentityPrincipalFactory, TestIdentityPrincipalFactory>();
        });
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
