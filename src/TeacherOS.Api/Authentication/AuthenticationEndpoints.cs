using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using TeacherOS.Api.Authorization;
using TeacherOS.Api.Errors;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;
using TeacherOS.Domain.Authorization;
using TeacherOS.Infrastructure.Identity;

namespace TeacherOS.Api.Authentication;

internal static class AuthenticationEndpoints
{
    internal static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapGet("/antiforgery", GetAntiforgeryToken)
            .Produces<AntiforgeryTokenResponse>()
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationConstants.LoginRateLimitPolicy)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapGet("/me", GetCurrentSessionAsync)
            .Produces<CurrentSessionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        group.MapPost("/logout", (Func<HttpContext, Task<IResult>>)LogoutAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult GetAntiforgeryToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        httpContext.Response.Headers.CacheControl = "no-store";

        return TypedResults.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler loginHandler,
        IIdentityPrincipalFactory principalFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        var principal = await principalFactory.CreateAsync(result.Value.UserId, cancellationToken);

        if (principal is null)
        {
            return ApiProblemDetails.FromError(AuthenticationErrors.SessionUnavailable);
        }

        await httpContext.SignInAsync(
            AuthenticationConstants.CookieScheme,
            principal,
            new AuthenticationProperties
            {
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                IsPersistent = true,
            });

        return TypedResults.Ok(new LoginResponse(result.Value.UserId, result.Value.Email));
    }

    private static async Task<IResult> GetCurrentSessionAsync(
        GetCurrentSessionHandler handler,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        var memberships = result.Value.Memberships
            .Select(membership => new TenantMembershipResponse(
                membership.TenantId,
                membership.TenantName,
                membership.TenantStatus.ToString(),
                membership.MembershipStatus.ToString()))
            .ToArray();

        return TypedResults.Ok(new CurrentSessionResponse(
            result.Value.UserId,
            result.Value.Email,
            tenantContext.IsAvailable ? tenantContext.TenantId : null,
            memberships));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(AuthenticationConstants.CookieScheme);
        return TypedResults.NoContent();
    }
}
