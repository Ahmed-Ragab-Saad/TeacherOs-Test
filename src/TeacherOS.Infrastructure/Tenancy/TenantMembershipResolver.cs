using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Tenancy;

internal sealed class TenantMembershipResolver(ApplicationDbContext dbContext)
    : ITenantMembershipResolver
{
    public Task<bool> HasActiveMembershipAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return dbContext.TenantMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.TenantId == tenantId &&
                    membership.Status == TenantMembershipStatus.Active,
                cancellationToken);
    }
}
