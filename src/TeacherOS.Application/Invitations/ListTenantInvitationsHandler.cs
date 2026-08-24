using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Invitations;

public sealed class ListTenantInvitationsHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantInvitationStore invitationStore)
{
    public async Task<Result<IReadOnlyList<TenantInvitationListItem>>> HandleAsync(
        ListTenantInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAuthenticated)
        {
            return Result<IReadOnlyList<TenantInvitationListItem>>.Failure(
                new Error("Authentication.Unauthorized", "Authentication is required.", ErrorType.Unauthorized));
        }

        if (!tenantContext.IsAvailable || tenantContext.TenantId != query.TenantId)
        {
            return Result<IReadOnlyList<TenantInvitationListItem>>.Failure(
                new Error("Tenancy.AccessDenied", "Access to the selected tenant is denied.", ErrorType.Forbidden));
        }

        var items = await invitationStore.ListByTenantIdAsync(query.TenantId, cancellationToken);
        return Result<IReadOnlyList<TenantInvitationListItem>>.Success(items);
    }
}
