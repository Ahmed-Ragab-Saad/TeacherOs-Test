using System;

namespace TeacherOS.Application.Invitations;

public sealed record RevokeTenantInvitationCommand(Guid TenantId, Guid InvitationId);
