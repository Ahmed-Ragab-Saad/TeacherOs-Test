namespace TeacherOS.Api.Authentication;

internal sealed record TenantMembershipResponse(
    Guid TenantId,
    string TenantName,
    string TenantStatus,
    string MembershipStatus);
