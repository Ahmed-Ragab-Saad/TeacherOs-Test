namespace TeacherOS.Application.Abstractions.Invitations;

public interface IInvitationTokenService
{
    string GenerateRawToken();
    string HashToken(string rawToken);
    string ProtectToken(string rawToken);
    string UnprotectToken(string protectedToken);
}
