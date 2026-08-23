namespace TeacherOS.Application.Authentication;

public sealed record RegisterCommand(string? Email, string? Password, string? TenantName);
