using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TeacherOS.Api.Authentication;
using TeacherOS.Api.Authorization;
using TeacherOS.Api.Errors;
using TeacherOS.Application.Memberships;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Api.Memberships;

internal static class MembershipEndpoints
{
    internal static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants/{tenantId:guid}/members")
            .WithTags("Tenant Memberships")
            .RequirePermission(Permission.MembersManage);

        group.MapGet("/", ListMembersAsync)
            .Produces<TenantMemberResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/{membershipId:guid}/status", UpdateMembershipStatusAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return endpoints;
    }

    private static async Task<IResult> ListMembersAsync(
        Guid tenantId,
        ListTenantMembersHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListTenantMembersQuery(tenantId),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        var response = result.Value
            .Select(m => new TenantMemberResponse(
                m.MembershipId,
                m.UserId,
                m.Email,
                m.RoleId,
                m.RoleName,
                m.Status))
            .ToArray();

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> UpdateMembershipStatusAsync(
        Guid tenantId,
        Guid membershipId,
        UpdateMembershipStatusRequest request,
        UpdateTenantMembershipStatusHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TenantMembershipStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return ApiProblemDetails.FromError(MembershipErrors.InvalidStatus);
        }

        var result = await handler.HandleAsync(
            new UpdateTenantMembershipStatusCommand(tenantId, membershipId, newStatus),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        return TypedResults.NoContent();
    }
}
