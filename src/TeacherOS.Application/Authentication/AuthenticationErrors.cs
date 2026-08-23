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

    public static Error InvalidEmail { get; } = new(
        "Authentication.InvalidEmail",
        "A valid email address is required.",
        ErrorType.Validation);

    public static Error PasswordRequired { get; } = new(
        "Authentication.PasswordRequired",
        "A password is required.",
        ErrorType.Validation);

    public static Error TenantNameRequired { get; } = new(
        "Authentication.TenantNameRequired",
        "A tenant name is required.",
        ErrorType.Validation);

    public static Error TenantNameTooLong { get; } = new(
        "Authentication.TenantNameTooLong",
        $"Tenant name cannot exceed {TeacherOS.Domain.Tenancy.Tenant.MaxNameLength} characters.",
        ErrorType.Validation);

    public static Error DuplicateEmail { get; } = new(
        "Authentication.DuplicateEmail",
        "A user with this email already exists.",
        ErrorType.Conflict);
}
