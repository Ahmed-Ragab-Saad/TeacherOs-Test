using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Authentication;

public sealed record CurrentTenantMembership(
    Guid TenantId,
    string TenantName,
    TenantStatus TenantStatus,
    TenantMembershipStatus MembershipStatus);
