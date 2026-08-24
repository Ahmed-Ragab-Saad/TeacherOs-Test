using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Invitations;

public sealed class RevokeTenantInvitationHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantInvitationStore invitationStore,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(
        RevokeTenantInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure(
                new Error("Authentication.Unauthorized", "Authentication is required.", ErrorType.Unauthorized));
        }

        if (!tenantContext.IsAvailable || tenantContext.TenantId != command.TenantId)
        {
            return Result.Failure(
                new Error("Tenancy.AccessDenied", "Access to the selected tenant is denied.", ErrorType.Forbidden));
        }

        var invitation = await invitationStore.GetByIdAsync(command.InvitationId, command.TenantId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure(InvitationErrors.NotFound);
        }

        if (invitation.IsAccepted)
        {
            return Result.Failure(InvitationErrors.AlreadyAccepted);
        }

        if (invitation.IsRevoked)
        {
            return Result.Failure(InvitationErrors.Revoked);
        }

        var utcNow = timeProvider.GetUtcNow();
        invitation.Revoke(utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
