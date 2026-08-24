using System;

namespace TeacherOS.Application.Invitations;

public sealed record AcceptTenantInvitationResult(
    Guid TenantId,
    Guid UserId,
    string Email,
    bool IsNewUser);
