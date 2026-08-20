using System.Threading;
using System.Threading.Tasks;

namespace TeacherOS.Application.Abstractions.Data;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
