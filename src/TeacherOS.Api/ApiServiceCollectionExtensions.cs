using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TeacherOS.Api.Authentication;
using TeacherOS.Api.Authorization;
using TeacherOS.Api.Errors;
using TeacherOS.Api.Observability;
using TeacherOS.Api.Tenancy;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Observability;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Authentication;

namespace TeacherOS.Api;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddOpenApi();
        services.AddHttpContextAccessor();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var problemDetails = context.ProblemDetails;
                var statusCode = problemDetails.Status ?? context.HttpContext.Response.StatusCode;

                problemDetails.Extensions.TryAdd("code", GetProblemCode(statusCode));
                problemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);

                if (context.HttpContext.Items.TryGetValue(
                        CorrelationIdMiddleware.HttpContextItemName,
                        out var correlationId))
                {
                    problemDetails.Extensions.TryAdd("correlationId", correlationId);
                }
            };
        });

        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(serviceProvider =>
            serviceProvider.GetRequiredService<CorrelationContext>());

        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<GetCurrentSessionHandler>();
        services.AddScoped<TeacherOS.Application.Invitations.CreateTenantInvitationHandler>();
        services.AddScoped<TeacherOS.Application.Invitations.ListTenantInvitationsHandler>();
        services.AddScoped<TeacherOS.Application.Invitations.RevokeTenantInvitationHandler>();
        services.AddScoped<TeacherOS.Application.Invitations.InspectTenantInvitationHandler>();
        services.AddScoped<TeacherOS.Application.Invitations.AcceptTenantInvitationHandler>();
        services.AddScoped<TeacherOS.Application.Memberships.ListTenantMembersHandler>();
        services.AddScoped<TeacherOS.Application.Memberships.UpdateTenantMembershipStatusHandler>();

        services.AddAuthentication(AuthenticationConstants.CookieScheme)
            .AddCookie(AuthenticationConstants.CookieScheme, options =>
            {
                options.Cookie.Name = "__Host-TeacherOS.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context => ApiProblemDetails.Create(
                            StatusCodes.Status401Unauthorized,
                            "Authentication.Unauthorized",
                            "Authentication is required.")
                        .ExecuteAsync(context.HttpContext),
                    OnRedirectToAccessDenied = context => ApiProblemDetails.Create(
                            StatusCodes.Status403Forbidden,
                            "Authorization.Forbidden",
                            "Access is forbidden.")
                        .ExecuteAsync(context.HttpContext),
                    OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync,
                };
            });

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-TeacherOS.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(
                ApiProblemDetails.Create(
                        StatusCodes.Status429TooManyRequests,
                        "Authentication.RateLimitExceeded",
                        "Too many login attempts. Try again later.")
                    .ExecuteAsync(context.HttpContext));

            options.AddPolicy(
                AuthenticationConstants.LoginRateLimitPolicy,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));

            options.AddPolicy(
                AuthenticationConstants.RegisterRateLimitPolicy,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));

            options.AddPolicy(
                AuthenticationConstants.InvitationCreateRateLimitPolicy,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));

            options.AddPolicy(
                AuthenticationConstants.InvitationInspectRateLimitPolicy,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 30,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));

            options.AddPolicy(
                AuthenticationConstants.InvitationAcceptRateLimitPolicy,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                    }));
        });

        services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"]);

        return services;
    }

    private static string GetProblemCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Http.BadRequest",
            StatusCodes.Status401Unauthorized => "Authentication.Unauthorized",
            StatusCodes.Status403Forbidden => "Authorization.Forbidden",
            StatusCodes.Status404NotFound => "Http.NotFound",
            StatusCodes.Status405MethodNotAllowed => "Http.MethodNotAllowed",
            StatusCodes.Status409Conflict => "Http.Conflict",
            StatusCodes.Status422UnprocessableEntity => "Http.UnprocessableEntity",
            StatusCodes.Status429TooManyRequests => "Http.TooManyRequests",
            StatusCodes.Status500InternalServerError => "Server.Unexpected",
            _ => "Http.Error",
        };
    }
}
