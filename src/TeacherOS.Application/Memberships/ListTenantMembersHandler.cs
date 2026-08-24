using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Memberships;

public sealed class ListTenantMembersHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantMembershipManagementStore membershipStore)
{
    public async Task<Result<IReadOnlyList<TenantMemberListItem>>> HandleAsync(
        ListTenantMembersQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAuthenticated)
        {
            return Result<IReadOnlyList<TenantMemberListItem>>.Failure(
                new Error("Authentication.Unauthorized", "Authentication is required.", ErrorType.Unauthorized));
        }

        if (!tenantContext.IsAvailable || tenantContext.TenantId != query.TenantId)
        {
            return Result<IReadOnlyList<TenantMemberListItem>>.Failure(
                new Error("Tenancy.AccessDenied", "Access to the selected tenant is denied.", ErrorType.Forbidden));
        }

        var members = await membershipStore.ListMembersAsync(query.TenantId, cancellationToken);
        return Result<IReadOnlyList<TenantMemberListItem>>.Success(members);
    }
}
