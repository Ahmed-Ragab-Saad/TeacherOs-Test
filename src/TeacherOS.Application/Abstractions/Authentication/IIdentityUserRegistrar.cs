using TeacherOS.Application.Common;

namespace TeacherOS.Application.Abstractions.Authentication;

public interface IIdentityUserRegistrar
{
    Task<Result<IdentityRegistrationResult>> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
