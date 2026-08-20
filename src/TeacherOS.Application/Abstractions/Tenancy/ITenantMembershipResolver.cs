namespace TeacherOS.Application.Abstractions.Tenancy;

public interface ITenantMembershipResolver
{
    Task<bool> HasActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken);
}
