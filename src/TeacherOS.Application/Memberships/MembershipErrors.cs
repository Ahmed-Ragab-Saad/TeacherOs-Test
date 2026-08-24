using TeacherOS.Application.Common;

namespace TeacherOS.Application.Memberships;

public static class MembershipErrors
{
    public static Error NotFound { get; } = new(
        "Memberships.NotFound",
        "The tenant membership was not found.",
        ErrorType.NotFound);

    public static Error CannotDisableLastOwner { get; } = new(
        "Memberships.CannotDisableLastOwner",
        "Cannot disable or suspend the last active owner of the tenant.",
        ErrorType.Conflict);

    public static Error InvalidStatus { get; } = new(
        "Memberships.InvalidStatus",
        "The specified membership status is invalid.",
        ErrorType.Validation);

    public static Error AlreadyInStatus { get; } = new(
        "Memberships.AlreadyInStatus",
        "The membership is already in the requested status.",
        ErrorType.Conflict);
}
