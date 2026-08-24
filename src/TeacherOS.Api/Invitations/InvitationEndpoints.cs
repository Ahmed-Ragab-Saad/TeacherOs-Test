using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using TeacherOS.Api.Authentication;
using TeacherOS.Api.Authorization;
using TeacherOS.Api.Errors;
using TeacherOS.Api.OpenApi;
using TeacherOS.Application.Invitations;
using TeacherOS.Domain.Authorization;

namespace TeacherOS.Api.Invitations;

internal static class InvitationEndpoints
{
    internal static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tenantGroup = endpoints.MapGroup("/api/tenants/{tenantId:guid}/invitations")
            .WithTags("Tenant Invitations")
            .RequireTenantContext()
            .RequirePermission(Permission.MembersManage);

        tenantGroup.MapGet("/", ListInvitationsAsync)
            .Produces<TenantInvitationResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        tenantGroup.MapPost("/", CreateInvitationAsync)
            .Produces<CreateTenantInvitationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(AuthenticationConstants.InvitationCreateRateLimitPolicy)
            .RequireAntiforgeryToken();

        tenantGroup.MapPost("/{invitationId:guid}/revoke", RevokeInvitationAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();

        var publicGroup = endpoints.MapGroup("/api/tenant-invitations")
            .WithTags("Public Tenant Invitations");

        publicGroup.MapPost("/inspect", InspectInvitationAsync)
            .Produces<TenantInvitationInspectionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationConstants.InvitationInspectRateLimitPolicy);

        publicGroup.MapPost("/accept", AcceptInvitationAsync)
            .Produces<AcceptTenantInvitationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationConstants.InvitationAcceptRateLimitPolicy)
            .RequireAntiforgeryToken();

        return endpoints;
    }

    private static async Task<IResult> ListInvitationsAsync(
        Guid tenantId,
        ListTenantInvitationsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListTenantInvitationsQuery(tenantId),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        var response = result.Value
            .Select(i => new TenantInvitationResponse(
                i.InvitationId,
                i.Email,
                i.RoleId,
                i.RoleName,
                i.CreatedAtUtc,
                i.ExpiresAtUtc,
                i.AcceptedAtUtc,
                i.RevokedAtUtc,
                i.Status))
            .ToArray();

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CreateInvitationAsync(
        Guid tenantId,
        CreateTenantInvitationRequest request,
        CreateTenantInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateTenantInvitationCommand(tenantId, request.Email, request.RoleId),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        return TypedResults.Created(
            $"/api/tenants/{tenantId}/invitations",
            new CreateTenantInvitationResponse(
                result.Value.InvitationId,
                result.Value.ExpiresAtUtc,
                result.Value.DeliveryStatus));
    }

    private static async Task<IResult> RevokeInvitationAsync(
        Guid tenantId,
        Guid invitationId,
        RevokeTenantInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RevokeTenantInvitationCommand(tenantId, invitationId),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> InspectInvitationAsync(
        InspectTenantInvitationRequest request,
        InspectTenantInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new InspectTenantInvitationQuery(request.Token),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        return TypedResults.Ok(new TenantInvitationInspectionResponse(
            result.Value.TenantName,
            result.Value.Email,
            result.Value.RoleName,
            result.Value.ExpiresAtUtc,
            result.Value.Status));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptTenantInvitationRequest request,
        AcceptTenantInvitationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AcceptTenantInvitationCommand(request.Token, request.Password),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        return TypedResults.Ok(new AcceptTenantInvitationResponse(
            result.Value.TenantId,
            result.Value.UserId,
            result.Value.Email,
            result.Value.IsNewUser));
    }
}
