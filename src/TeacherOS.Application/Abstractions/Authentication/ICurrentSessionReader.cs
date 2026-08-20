using TeacherOS.Application.Authentication;

namespace TeacherOS.Application.Abstractions.Authentication;

public interface ICurrentSessionReader
{
    Task<CurrentSession?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
