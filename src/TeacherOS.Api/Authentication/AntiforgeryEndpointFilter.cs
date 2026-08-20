using Microsoft.AspNetCore.Antiforgery;
using TeacherOS.Api.Errors;

namespace TeacherOS.Api.Authentication;

internal sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return ApiProblemDetails.Create(
                StatusCodes.Status400BadRequest,
                "Antiforgery.ValidationFailed",
                "Antiforgery validation failed.");
        }

        return await next(context);
    }
}
