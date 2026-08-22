using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeacherOS.Application.Abstractions.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Authorization;

internal sealed class PermissionResolver(ApplicationDbContext dbContext) : IPermissionResolver
{
    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var permissionCodes = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == userId &&
                membership.TenantId == tenantId &&
                membership.Status == TenantMembershipStatus.Active &&
                membership.RoleId != null)
            .Join(
                dbContext.Roles.AsNoTracking(),
                membership => membership.RoleId,
                role => role.Id,
                (_, role) => role.PermissionCodes)
            .FirstOrDefaultAsync(cancellationToken);

        return permissionCodes ?? Array.Empty<string>();
    }
}
