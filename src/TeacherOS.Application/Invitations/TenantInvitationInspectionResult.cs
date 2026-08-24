using System;

namespace TeacherOS.Application.Invitations;

public sealed record TenantInvitationInspectionResult(
    string TenantName,
    string Email,
    string? RoleName,
    DateTimeOffset ExpiresAtUtc,
    string Status);
