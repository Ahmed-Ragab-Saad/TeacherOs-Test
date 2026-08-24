using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Invitations;

public sealed class InspectTenantInvitationHandler(
    ITenantInvitationStore invitationStore,
    ITenantMembershipManagementStore membershipStore,
    IInvitationTokenService tokenService,
    TimeProvider timeProvider)
{
    public async Task<Result<TenantInvitationInspectionResult>> HandleAsync(
        InspectTenantInvitationQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return Result<TenantInvitationInspectionResult>.Failure(InvitationErrors.NotFound);
        }

        var tokenHash = tokenService.HashToken(query.Token.Trim());
        var invitation = await invitationStore.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (invitation is null)
        {
            return Result<TenantInvitationInspectionResult>.Failure(InvitationErrors.NotFound);
        }

        var utcNow = timeProvider.GetUtcNow();
        var status = invitation.IsRevoked
            ? "Revoked"
            : invitation.IsAccepted
                ? "Accepted"
                : invitation.IsExpired(utcNow)
                    ? "Expired"
                    : "Pending";

        var tenantName = await membershipStore.GetTenantNameAsync(invitation.TenantId, cancellationToken) ?? "Unknown Tenant";
        string? roleName = null;
        if (invitation.RoleId.HasValue)
        {
            roleName = await membershipStore.GetRoleNameAsync(invitation.RoleId.Value, cancellationToken);
        }

        return Result<TenantInvitationInspectionResult>.Success(
            new TenantInvitationInspectionResult(
                tenantName,
                invitation.Email,
                roleName,
                invitation.ExpiresAtUtc,
                status));
    }
}
