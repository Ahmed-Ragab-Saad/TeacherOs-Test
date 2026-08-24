using System;

namespace TeacherOS.Api.Invitations;

public sealed record CreateTenantInvitationRequest(string Email, Guid? RoleId);
