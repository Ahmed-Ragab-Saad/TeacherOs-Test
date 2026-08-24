using System;

namespace TeacherOS.Application.Abstractions.Invitations;

public sealed record TenantInvitationListItem(
    Guid InvitationId,
    string Email,
    Guid? RoleId,
    string? RoleName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string Status);
