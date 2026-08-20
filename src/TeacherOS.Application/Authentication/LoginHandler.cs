using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Authentication;

public sealed class LoginHandler(IIdentityAuthenticator identityAuthenticator)
{
    public async Task<Result<IdentityAuthenticationResult>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var email = command.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(command.Password))
        {
            return Result<IdentityAuthenticationResult>.Failure(AuthenticationErrors.CredentialsRequired);
        }

        var authenticatedUser = await identityAuthenticator.AuthenticateAsync(
            email,
            command.Password,
            cancellationToken);

        return authenticatedUser is null
            ? Result<IdentityAuthenticationResult>.Failure(AuthenticationErrors.InvalidCredentials)
            : Result<IdentityAuthenticationResult>.Success(authenticatedUser);
    }
}
