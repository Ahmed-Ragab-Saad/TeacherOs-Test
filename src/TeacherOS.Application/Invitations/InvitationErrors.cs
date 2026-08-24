using TeacherOS.Application.Common;

namespace TeacherOS.Application.Invitations;

public static class InvitationErrors
{
    public static Error InvalidEmail { get; } = new(
        "Invitations.InvalidEmail",
        "A valid email address is required.",
        ErrorType.Validation);

    public static Error InvalidRole { get; } = new(
        "Invitations.InvalidRole",
        "The specified role does not exist or is not available for this tenant.",
        ErrorType.Validation);

    public static Error MemberAlreadyExists { get; } = new(
        "Invitations.MemberAlreadyExists",
        "A member with this email already exists in this tenant.",
        ErrorType.Conflict);

    public static Error PendingInvitationExists { get; } = new(
        "Invitations.PendingInvitationExists",
        "A pending invitation already exists for this email address.",
        ErrorType.Conflict);

    public static Error NotFound { get; } = new(
        "Invitations.NotFound",
        "The invitation was not found.",
        ErrorType.NotFound);

    public static Error Expired { get; } = new(
        "Invitations.Expired",
        "The invitation has expired.",
        ErrorType.Conflict);

    public static Error Revoked { get; } = new(
        "Invitations.Revoked",
        "The invitation has been revoked.",
        ErrorType.Conflict);

    public static Error AlreadyAccepted { get; } = new(
        "Invitations.AlreadyAccepted",
        "The invitation has already been accepted.",
        ErrorType.Conflict);

    public static Error EmailMismatch { get; } = new(
        "Invitations.EmailMismatch",
        "The authenticated user's email does not match the invitation email.",
        ErrorType.Conflict);

    public static Error TenantInactive { get; } = new(
        "Invitations.TenantInactive",
        "The tenant is not active.",
        ErrorType.Conflict);

    public static Error PasswordRequired { get; } = new(
        "Invitations.PasswordRequired",
        "A password is required to create an account.",
        ErrorType.Validation);
}
