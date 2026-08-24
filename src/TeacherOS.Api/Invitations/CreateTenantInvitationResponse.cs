using System;

namespace TeacherOS.Api.Invitations;

public sealed record CreateTenantInvitationResponse(
    Guid InvitationId,
    DateTimeOffset ExpiresAtUtc,
    string DeliveryStatus);
