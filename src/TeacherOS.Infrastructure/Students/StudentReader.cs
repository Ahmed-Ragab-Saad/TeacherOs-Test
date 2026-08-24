using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Students;
using TeacherOS.Application.Students;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Students;

internal sealed class StudentReader(ApplicationDbContext dbContext) : IStudentReader
{
    public async Task<IReadOnlyList<StudentListItem>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from student in dbContext.Students.AsNoTracking()
            join branch in dbContext.Branches.AsNoTracking() on student.BranchId equals branch.Id
            join gradeLevel in dbContext.GradeLevels.AsNoTracking() on student.GradeLevelId equals gradeLevel.Id
            where student.TenantId == tenantId
            orderby student.FullName, student.Id
            select new StudentListItem(
                student.Id,
                student.StudentCode,
                student.FullName,
                student.NationalId,
                branch.Id,
                branch.Name,
                gradeLevel.Id,
                gradeLevel.Name,
                student.Status,
                student.EnrollmentDate,
                student.PhoneNumber,
                student.PhotoUrl))
            .ToListAsync(cancellationToken);
    }
}
