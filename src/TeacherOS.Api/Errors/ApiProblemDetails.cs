using TeacherOS.Application.Common;

namespace TeacherOS.Api.Errors;

internal static class ApiProblemDetails
{
    internal static IResult FromError(Error error)
    {
        return Create(
            GetStatusCode(error.Type),
            error.Code,
            error.Description);
    }

    internal static IResult Create(int statusCode, string code, string detail)
    {
        return Results.Problem(
            statusCode: statusCode,
            title: GetTitle(statusCode),
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
    }

    private static int GetStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "Access is forbidden.",
            StatusCodes.Status404NotFound => "The resource was not found.",
            StatusCodes.Status409Conflict => "The request conflicts with the current state.",
            StatusCodes.Status429TooManyRequests => "Too many requests.",
            _ => "An unexpected error occurred.",
        };
    }
}
