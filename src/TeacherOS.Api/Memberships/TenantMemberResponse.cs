using System;

namespace TeacherOS.Api.Memberships;

public sealed record TenantMemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    Guid? RoleId,
    string? RoleName,
    string Status);
