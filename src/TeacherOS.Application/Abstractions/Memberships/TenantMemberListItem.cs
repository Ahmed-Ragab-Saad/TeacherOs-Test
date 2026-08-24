using System;

namespace TeacherOS.Application.Abstractions.Memberships;

public sealed record TenantMemberListItem(
    Guid MembershipId,
    Guid UserId,
    string Email,
    Guid? RoleId,
    string? RoleName,
    string Status);
