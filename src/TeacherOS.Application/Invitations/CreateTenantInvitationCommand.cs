using System;

namespace TeacherOS.Application.Invitations;

public sealed record CreateTenantInvitationCommand(
    Guid TenantId,
    string Email,
    Guid? RoleId,
    TimeSpan? ValidFor = null);
