using System;

namespace TeacherOS.Application.Invitations;

public sealed record CreateTenantInvitationResult(
    Guid InvitationId,
    DateTimeOffset ExpiresAtUtc,
    string DeliveryStatus);
