using Microsoft.Extensions.Primitives;
using TeacherOS.Api.Errors;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Tenancy;

namespace TeacherOS.Api.Tenancy;

internal sealed class TenantContextMiddleware(RequestDelegate next)
{
    internal const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(
        HttpContext httpContext,
        ICurrentUser currentUser,
        ITenantMembershipResolver membershipResolver,
        ITenantContextEstablisher tenantContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(TenantHeaderName, out var values))
        {
            await next(httpContext);
            return;
        }

        if (!currentUser.IsAuthenticated)
        {
            await next(httpContext);
            return;
        }

        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            await ApiProblemDetails.Create(
                StatusCodes.Status401Unauthorized,
                "Authentication.SessionUnavailable",
                "The authenticated session is unavailable.")
                .ExecuteAsync(httpContext);
            return;
        }

        if (!TryParseTenantId(values, out var tenantId))
        {
            await ApiProblemDetails.Create(
                StatusCodes.Status400BadRequest,
                "Tenancy.InvalidSelector",
                $"{TenantHeaderName} must contain exactly one non-empty tenant identifier.")
                .ExecuteAsync(httpContext);
            return;
        }

        var isActiveMember = await membershipResolver.HasActiveMembershipAsync(
            userId,
            tenantId,
            httpContext.RequestAborted);

        if (!isActiveMember)
        {
            await ApiProblemDetails.Create(
                StatusCodes.Status403Forbidden,
                "Tenancy.AccessDenied",
                "The selected tenant is not available to the current user.")
                .ExecuteAsync(httpContext);
            return;
        }

        tenantContext.Establish(tenantId);
        await next(httpContext);
    }

    private static bool TryParseTenantId(StringValues values, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        return values.Count == 1 &&
            Guid.TryParse(values[0], out tenantId) &&
            tenantId != Guid.Empty;
    }
}
