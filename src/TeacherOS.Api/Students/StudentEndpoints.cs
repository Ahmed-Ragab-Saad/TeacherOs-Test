using Microsoft.AspNetCore.Routing;
using TeacherOS.Api.Errors;
using TeacherOS.Api.OpenApi;
using TeacherOS.Application.Students;
using TeacherOS.Domain.Students;

namespace TeacherOS.Api.Students;

internal static class StudentEndpoints
{
    internal static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapBranchEndpoints(endpoints);
        MapGradeLevelEndpoints(endpoints);
        MapStudentRoutes(endpoints);
        return endpoints;
    }

    private static void MapBranchEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants/{tenantId:guid}/branches")
            .WithTags("Branches")
            .RequireTenantContext();

        group.MapGet("", ListBranchesAsync)
            .Produces<BranchResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("", CreateBranchAsync)
            .Produces<BranchResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();

        group.MapGet("/{branchId:guid}", GetBranchAsync)
            .Produces<BranchResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{branchId:guid}", UpdateBranchAsync)
            .Produces<BranchResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();
    }

    private static void MapGradeLevelEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants/{tenantId:guid}/grade-levels")
            .WithTags("Grade Levels")
            .RequireTenantContext();

        group.MapGet("", ListGradeLevelsAsync)
            .Produces<GradeLevelResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("", CreateGradeLevelAsync)
            .Produces<GradeLevelResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();

        group.MapGet("/{gradeLevelId:guid}", GetGradeLevelAsync)
            .Produces<GradeLevelResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{gradeLevelId:guid}", UpdateGradeLevelAsync)
            .Produces<GradeLevelResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();
    }

    private static void MapStudentRoutes(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants/{tenantId:guid}/students")
            .WithTags("Students")
            .RequireTenantContext();

        group.MapGet("", ListStudentsAsync)
            .Produces<StudentListResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("", CreateStudentAsync)
            .Produces<StudentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();

        group.MapGet("/{studentId:guid}", GetStudentAsync)
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{studentId:guid}", UpdateStudentAsync)
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();

        group.MapPut("/{studentId:guid}/branch", AssignBranchAsync)
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAntiforgeryToken();

        group.MapPut("/{studentId:guid}/grade-level", AssignGradeLevelAsync)
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAntiforgeryToken();

        MapStudentStatusEndpoint(
            group,
            "/{studentId:guid}/suspensions/administrative",
            SuspendAdministrativelyAsync);
        MapStudentStatusEndpoint(
            group,
            "/{studentId:guid}/suspensions/non-payment",
            SuspendForNonPaymentAsync);
        MapStudentStatusEndpoint(group, "/{studentId:guid}/reactivation", ReactivateAsync);
        MapStudentStatusEndpoint(group, "/{studentId:guid}/graduation", GraduateAsync);
    }

    private static void MapStudentStatusEndpoint(
        RouteGroupBuilder group,
        string pattern,
        Delegate handler)
    {
        group.MapPost(pattern, handler)
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAntiforgeryToken();
    }

    private static async Task<IResult> ListBranchesAsync(Guid tenantId, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.ListBranchesAsync(tenantId, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(result.Value.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetBranchAsync(Guid tenantId, Guid branchId, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.GetBranchAsync(tenantId, branchId, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> CreateBranchAsync(Guid tenantId, BranchWriteRequest request, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.CreateBranchAsync(tenantId, request.Name, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Created($"/api/tenants/{tenantId}/branches/{result.Value.Id}", ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateBranchAsync(Guid tenantId, Guid branchId, BranchWriteRequest request, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateBranchAsync(tenantId, branchId, request.Name, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ListGradeLevelsAsync(Guid tenantId, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.ListGradeLevelsAsync(tenantId, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(result.Value.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> GetGradeLevelAsync(Guid tenantId, Guid gradeLevelId, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.GetGradeLevelAsync(tenantId, gradeLevelId, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> CreateGradeLevelAsync(Guid tenantId, GradeLevelWriteRequest request, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.CreateGradeLevelAsync(tenantId, request.Name, request.SortOrder, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Created($"/api/tenants/{tenantId}/grade-levels/{result.Value.Id}", ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateGradeLevelAsync(Guid tenantId, Guid gradeLevelId, GradeLevelWriteRequest request, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateGradeLevelAsync(tenantId, gradeLevelId, request.Name, request.SortOrder, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ListStudentsAsync(Guid tenantId, ListStudentsHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListStudentsQuery(tenantId), cancellationToken);
        if (result.IsFailure) return ApiProblemDetails.FromError(result.Error);
        return TypedResults.Ok(result.Value.Select(student => new StudentListResponse(student.Id, student.StudentCode, student.FullName, student.NationalId, student.BranchId, student.BranchName, student.GradeLevelId, student.GradeLevelName, student.Status, student.EnrollmentDate, student.PhoneNumber, student.PhotoUrl)).ToArray());
    }

    private static async Task<IResult> GetStudentAsync(Guid tenantId, Guid studentId, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.GetStudentAsync(tenantId, studentId, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> CreateStudentAsync(Guid tenantId, StudentCreateRequest request, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.CreateStudentAsync(tenantId, request.StudentCode, request.FullName, request.NationalId, request.BranchId, request.GradeLevelId, request.EnrollmentDate, request.PhoneNumber, request.PhotoUrl, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Created($"/api/tenants/{tenantId}/students/{result.Value.Id}", ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateStudentAsync(Guid tenantId, Guid studentId, StudentUpdateRequest request, StudentManagementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateStudentAsync(tenantId, studentId, request.FullName, request.NationalId, request.BranchId, request.GradeLevelId, request.EnrollmentDate, request.PhoneNumber, request.PhotoUrl, cancellationToken);
        return result.IsFailure ? ApiProblemDetails.FromError(result.Error) : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> AssignBranchAsync(
        Guid tenantId,
        Guid studentId,
        StudentBranchAssignmentRequest request,
        StudentManagementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.AssignBranchAsync(
            tenantId,
            studentId,
            request.BranchId,
            cancellationToken);

        return result.IsFailure
            ? ApiProblemDetails.FromError(result.Error)
            : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> AssignGradeLevelAsync(
        Guid tenantId,
        Guid studentId,
        StudentGradeLevelAssignmentRequest request,
        StudentManagementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.AssignGradeLevelAsync(
            tenantId,
            studentId,
            request.GradeLevelId,
            cancellationToken);

        return result.IsFailure
            ? ApiProblemDetails.FromError(result.Error)
            : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> SuspendAdministrativelyAsync(
        Guid tenantId,
        Guid studentId,
        StudentManagementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.SuspendAdministrativelyAsync(tenantId, studentId, cancellationToken);
        return result.IsFailure
            ? ApiProblemDetails.FromError(result.Error)
            : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> SuspendForNonPaymentAsync(
        Guid tenantId,
        Guid studentId,
        StudentManagementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.SuspendForNonPaymentAsync(tenantId, studentId, cancellationToken);
        return result.IsFailure
            ? ApiProblemDetails.FromError(result.Error)
            : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ReactivateAsync(
        Guid tenantId,
        Guid studentId,
        StudentManagementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ReactivateAsync(tenantId, studentId, cancellationToken);
        return result.IsFailure
            ? ApiProblemDetails.FromError(result.Error)
            : TypedResults.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> GraduateAsync(
        Guid tenantId,
        Guid studentId,
        StudentManagementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GraduateAsync(tenantId, studentId, cancellationToken);
        return result.IsFailure
            ? ApiProblemDetails.FromError(result.Error)
            : TypedResults.Ok(ToResponse(result.Value));
    }

    private static BranchResponse ToResponse(Branch branch) => new(branch.Id, branch.Name);
    private static GradeLevelResponse ToResponse(GradeLevel gradeLevel) => new(gradeLevel.Id, gradeLevel.Name, gradeLevel.SortOrder);
    private static StudentResponse ToResponse(Student student) => new(student.Id, student.StudentCode, student.FullName, student.NationalId, student.BranchId, student.GradeLevelId, student.Status, student.EnrollmentDate, student.PhoneNumber, student.PhotoUrl);
}
