using TeacherOS.Application.Common;

namespace TeacherOS.Application.Authentication;

public static class AuthenticationErrors
{
    public static Error CredentialsRequired { get; } = new(
        "Authentication.CredentialsRequired",
        "Email and password are required.",
        ErrorType.Validation);

    public static Error InvalidCredentials { get; } = new(
        "Authentication.InvalidCredentials",
        "The email or password is invalid.",
        ErrorType.Unauthorized);

    public static Error SessionUnavailable { get; } = new(
        "Authentication.SessionUnavailable",
        "The authenticated session is unavailable.",
        ErrorType.Unauthorized);
}
