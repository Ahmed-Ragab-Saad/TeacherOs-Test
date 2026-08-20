using System.Linq;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Authentication;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Identity;

internal sealed class CurrentSessionReader(ApplicationDbContext dbContext) : ICurrentSessionReader
{
    public async Task<CurrentSession?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Email,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var membershipRows = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                dbContext.Tenants.AsNoTracking(),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, tenant) => new
                {
                    Membership = membership,
                    Tenant = tenant,
                })
            .OrderBy(result => result.Tenant.Name)
            .Select(result => new
            {
                TenantId = result.Tenant.Id,
                TenantName = result.Tenant.Name,
                result.Tenant.Status,
                MembershipStatus = result.Membership.Status,
            })
            .ToArrayAsync(cancellationToken);

        var memberships = membershipRows
            .Select(row => new CurrentTenantMembership(
                row.TenantId,
                row.TenantName,
                row.Status,
                row.MembershipStatus))
            .ToArray();

        return new CurrentSession(user.Id, user.Email ?? string.Empty, memberships);
    }
}
