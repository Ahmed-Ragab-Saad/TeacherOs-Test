using TeacherOS.Api.Errors;
using TeacherOS.Api.Observability;
using TeacherOS.Application.Abstractions.Observability;

namespace TeacherOS.Api;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddRouting(options => options.LowercaseUrls = true);
        services.AddOpenApi();
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
