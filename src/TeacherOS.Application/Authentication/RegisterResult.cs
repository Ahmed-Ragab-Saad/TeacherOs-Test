namespace TeacherOS.Application.Authentication;

public sealed record RegisterResult(Guid UserId, string Email, Guid TenantId);
