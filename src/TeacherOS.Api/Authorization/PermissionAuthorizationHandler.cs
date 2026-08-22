using Microsoft.AspNetCore.Authorization;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Authorization;
using TeacherOS.Application.Abstractions.Tenancy;

namespace TeacherOS.Api.Authorization;

internal sealed class PermissionAuthorizationHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IPermissionResolver permissionResolver) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated ||
            currentUser.UserId is not Guid userId ||
            !tenantContext.IsAvailable)
        {
            return;
        }

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        var permissions = await permissionResolver.GetPermissionsAsync(
            userId,
            tenantContext.TenantId,
            cancellationToken);

        if (permissions.Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
    }
}
