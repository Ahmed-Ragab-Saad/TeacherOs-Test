namespace TeacherOS.Api.Invitations;

public sealed record AcceptTenantInvitationRequest(string Token, string? Password = null);
