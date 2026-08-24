using System;

namespace TeacherOS.Api.Invitations;

public sealed record TenantInvitationInspectionResponse(
    string TenantName,
    string Email,
    string? RoleName,
    DateTimeOffset ExpiresAtUtc,
    string Status);
