using System;

namespace TeacherOS.Api.Invitations;

public sealed record AcceptTenantInvitationResponse(
    Guid TenantId,
    Guid UserId,
    string Email,
    bool IsNewUser);
