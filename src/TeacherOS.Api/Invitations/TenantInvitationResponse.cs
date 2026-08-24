using System;

namespace TeacherOS.Api.Invitations;

public sealed record TenantInvitationResponse(
    Guid InvitationId,
    string Email,
    Guid? RoleId,
    string? RoleName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string Status);
