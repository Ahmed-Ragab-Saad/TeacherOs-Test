using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Abstractions.Data;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default);
}
