namespace TeacherOS.Api.Authentication;

public sealed record RegisterRequest(string Email, string Password, string TenantName);
