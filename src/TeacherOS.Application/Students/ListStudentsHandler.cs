using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Students;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeacherOS.Application.Students;

public sealed class ListStudentsHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IStudentReader studentReader)
{
    public async Task<Result<IReadOnlyList<StudentListItem>>> HandleAsync(
        ListStudentsQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAuthenticated)
        {
            return Result<IReadOnlyList<StudentListItem>>.Failure(
                new Error("Authentication.Unauthorized", "Authentication is required.", ErrorType.Unauthorized));
        }

        if (!tenantContext.IsAvailable || tenantContext.TenantId != query.TenantId)
        {
            return Result<IReadOnlyList<StudentListItem>>.Failure(
                new Error("Tenancy.AccessDenied", "Access to the selected tenant is denied.", ErrorType.Forbidden));
        }

        var students = await studentReader.ListAsync(query.TenantId, cancellationToken);
        return Result<IReadOnlyList<StudentListItem>>.Success(students);
    }
}
