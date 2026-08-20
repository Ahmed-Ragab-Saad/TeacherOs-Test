namespace TeacherOS.Api.Authentication;

internal sealed record CurrentSessionResponse(
    Guid UserId,
    string Email,
    Guid? SelectedTenantId,
    IReadOnlyCollection<TenantMembershipResponse> Memberships);
