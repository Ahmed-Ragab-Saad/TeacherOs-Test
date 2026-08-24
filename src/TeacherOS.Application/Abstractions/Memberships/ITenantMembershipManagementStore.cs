using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Abstractions.Memberships;

public interface ITenantMembershipManagementStore
{
    Task<IReadOnlyList<TenantMemberListItem>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantMembership?> GetMembershipAsync(Guid membershipId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveMembershipAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken = default);
    Task<bool> HasActiveMembershipForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsRoleValidForTenantAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);
    Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberActiveOwnerAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken = default);
    Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<string?> GetUserEmailAsync(Guid userId, CancellationToken cancellationToken = default);
    void AddMembership(TenantMembership membership);
}
