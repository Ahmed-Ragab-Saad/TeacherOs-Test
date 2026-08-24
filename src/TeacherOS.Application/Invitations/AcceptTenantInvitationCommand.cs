namespace TeacherOS.Application.Invitations;

public sealed record AcceptTenantInvitationCommand(
    string Token,
    string? Password = null);
