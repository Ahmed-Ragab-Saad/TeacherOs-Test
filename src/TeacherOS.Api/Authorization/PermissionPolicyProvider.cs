using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TeacherOS.Api.Authorization;

internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "Permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            return _fallbackProvider.GetPolicyAsync(policyName);
        }

        var permission = policyName[PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
