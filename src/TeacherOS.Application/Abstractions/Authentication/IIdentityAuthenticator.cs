using TeacherOS.Application.Authentication;

namespace TeacherOS.Application.Abstractions.Authentication;

public interface IIdentityAuthenticator
{
    Task<IdentityAuthenticationResult?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
