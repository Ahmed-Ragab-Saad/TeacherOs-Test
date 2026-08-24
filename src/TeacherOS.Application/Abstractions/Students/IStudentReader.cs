using TeacherOS.Application.Students;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeacherOS.Application.Abstractions.Students;

public interface IStudentReader
{
    Task<IReadOnlyList<StudentListItem>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
