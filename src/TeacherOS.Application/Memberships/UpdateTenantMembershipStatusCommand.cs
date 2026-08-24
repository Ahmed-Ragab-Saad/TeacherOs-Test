using System;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Memberships;

public sealed record UpdateTenantMembershipStatusCommand(
    Guid TenantId,
    Guid MembershipId,
    TenantMembershipStatus NewStatus);
