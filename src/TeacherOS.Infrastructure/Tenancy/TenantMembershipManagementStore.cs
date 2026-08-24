using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Tenancy;

internal sealed class TenantMembershipManagementStore(ApplicationDbContext dbContext)
    : ITenantMembershipManagementStore
{
    public async Task<IReadOnlyList<TenantMemberListItem>> ListMembersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var userIds = memberships.Select(m => m.UserId).Distinct().ToList();
        var usersMap = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? string.Empty, cancellationToken);

        var roleIds = memberships.Where(m => m.RoleId.HasValue).Select(m => m.RoleId!.Value).Distinct().ToList();
        var rolesMap = roleIds.Count > 0
            ? await dbContext.Roles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        var result = new List<TenantMemberListItem>(memberships.Count);
        foreach (var m in memberships)
        {
            usersMap.TryGetValue(m.UserId, out var email);
            string? roleName = null;
            if (m.RoleId.HasValue)
            {
                rolesMap.TryGetValue(m.RoleId.Value, out roleName);
            }

            result.Add(new TenantMemberListItem(
                m.Id,
                m.UserId,
                email ?? string.Empty,
                m.RoleId,
                roleName,
                m.Status.ToString()));
        }

        return result;
    }

    public Task<TenantMembership?> GetMembershipAsync(
        Guid membershipId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TenantMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.TenantId == tenantId, cancellationToken);
    }

    public async Task<bool> HasActiveMembershipAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return await (from m in dbContext.TenantMemberships.AsNoTracking()
                      join u in dbContext.Users.AsNoTracking() on m.UserId equals u.Id
                      where m.TenantId == tenantId &&
                            m.Status == TenantMembershipStatus.Active &&
                            u.NormalizedEmail == normalizedEmail
                      select m.Id).AnyAsync(cancellationToken);
    }

    public Task<bool> HasActiveMembershipForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TenantMemberships
            .AsNoTracking()
            .AnyAsync(
                m => m.TenantId == tenantId &&
                     m.UserId == userId &&
                     m.Status == TenantMembershipStatus.Active,
                cancellationToken);
    }

    public Task<bool> IsRoleValidForTenantAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);
    }

    public async Task<int> CountActiveOwnersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var activeMembershipsQuery = dbContext.Database.IsSqlServer()
            ? dbContext.TenantMemberships
                .FromSqlInterpolated($@"
                    SELECT m.* FROM [TenantMemberships] m WITH (UPDLOCK, HOLDLOCK)
                    WHERE m.[TenantId] = {tenantId}
                      AND m.[Status] = {TenantMembershipStatus.Active}
                      AND m.[RoleId] IS NOT NULL")
            : dbContext.TenantMemberships
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId &&
                            m.Status == TenantMembershipStatus.Active &&
                            m.RoleId != null);

        var activeMembershipsWithRoles = await activeMembershipsQuery
            .Join(
                dbContext.Roles.AsNoTracking().Where(r => r.TenantId == tenantId),
                m => m.RoleId,
                r => r.Id,
                (m, r) => new { m.Id, r.Name, r.PermissionCodes })
            .ToListAsync(cancellationToken);

        return activeMembershipsWithRoles
            .Count(item => item.PermissionCodes.Contains(Permission.MembersManage, StringComparer.Ordinal) ||
                           string.Equals(item.Name, "Owner", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsMemberActiveOwnerAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.Id == membershipId &&
                     m.TenantId == tenantId &&
                     m.Status == TenantMembershipStatus.Active &&
                     m.RoleId != null,
                cancellationToken);

        if (membership is null || !membership.RoleId.HasValue)
        {
            return false;
        }

        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == membership.RoleId.Value && r.TenantId == tenantId,
                cancellationToken);

        if (role is null)
        {
            return false;
        }

        return role.PermissionCodes.Contains(Permission.MembersManage, StringComparer.Ordinal) ||
               string.Equals(role.Name, "Owner", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetTenantNameAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        return tenant?.Name;
    }

    public async Task<string?> GetRoleNameAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        return role?.Name;
    }

    public async Task<string?> GetUserEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user?.Email;
    }

    public void AddMembership(TenantMembership membership)
    {
        dbContext.TenantMemberships.Add(membership);
    }
}
