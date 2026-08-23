namespace TeacherOS.Api.Authentication;

public sealed record RegisterResponse(Guid UserId, string Email, Guid TenantId);
