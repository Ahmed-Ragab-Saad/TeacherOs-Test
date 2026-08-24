using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TeacherOS.Api.Errors;
using TeacherOS.Api.OpenApi;
using TeacherOS.Application.Students;

namespace TeacherOS.Api.Students;

internal static class StudentEndpoints
{
    internal static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants/{tenantId:guid}/students")
            .WithTags("Students")
            .RequireTenantContext();

        group.MapGet("/", ListStudentsAsync)
            .Produces<StudentListResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> ListStudentsAsync(
        Guid tenantId,
        ListStudentsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListStudentsQuery(tenantId), cancellationToken);
        if (result.IsFailure)
        {
            return ApiProblemDetails.FromError(result.Error);
        }

        var response = result.Value
            .Select(student => new StudentListResponse(
                student.Id,
                student.StudentCode,
                student.FullName,
                student.NationalId,
                student.BranchId,
                student.BranchName,
                student.GradeLevelId,
                student.GradeLevelName,
                student.Status,
                student.EnrollmentDate,
                student.PhoneNumber,
                student.PhotoUrl))
            .ToArray();

        return TypedResults.Ok(response);
    }
}
