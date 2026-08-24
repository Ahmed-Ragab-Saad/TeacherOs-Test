using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Invitations;

public sealed class CreateTenantInvitationHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantInvitationStore invitationStore,
    ITenantMembershipManagementStore membershipStore,
    IInvitationTokenService tokenService,
    IEmailOutboxProcessor emailOutboxProcessor,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<CreateTenantInvitationResult>> HandleAsync(
        CreateTenantInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return Result<CreateTenantInvitationResult>.Failure(
                new Error("Authentication.Unauthorized", "Authentication is required.", ErrorType.Unauthorized));
        }

        if (!tenantContext.IsAvailable || tenantContext.TenantId != command.TenantId)
        {
            return Result<CreateTenantInvitationResult>.Failure(
                new Error("Tenancy.AccessDenied", "Access to the selected tenant is denied.", ErrorType.Forbidden));
        }

        var rawEmail = command.Email?.Trim();
        if (string.IsNullOrWhiteSpace(rawEmail) || !EmailValidator.IsValid(rawEmail))
        {
            return Result<CreateTenantInvitationResult>.Failure(InvitationErrors.InvalidEmail);
        }

        var normalizedEmail = rawEmail.ToUpperInvariant();
        var utcNow = timeProvider.GetUtcNow();

        var isAlreadyMember = await membershipStore.HasActiveMembershipAsync(command.TenantId, normalizedEmail, cancellationToken);
        if (isAlreadyMember)
        {
            return Result<CreateTenantInvitationResult>.Failure(InvitationErrors.MemberAlreadyExists);
        }

        var hasPendingInvitation = await invitationStore.HasPendingInvitationAsync(command.TenantId, normalizedEmail, utcNow, cancellationToken);
        if (hasPendingInvitation)
        {
            return Result<CreateTenantInvitationResult>.Failure(InvitationErrors.PendingInvitationExists);
        }

        if (command.RoleId.HasValue)
        {
            var isRoleValid = await membershipStore.IsRoleValidForTenantAsync(command.TenantId, command.RoleId.Value, cancellationToken);
            if (!isRoleValid)
            {
                return Result<CreateTenantInvitationResult>.Failure(InvitationErrors.InvalidRole);
            }
        }

        var rawToken = tokenService.GenerateRawToken();
        var tokenHash = tokenService.HashToken(rawToken);
        var protectedToken = tokenService.ProtectToken(rawToken);

        var validDuration = command.ValidFor ?? TimeSpan.FromDays(7);
        var expiresAtUtc = utcNow.Add(validDuration);

        var invitation = new TenantInvitation(
            Guid.NewGuid(),
            command.TenantId,
            rawEmail,
            normalizedEmail,
            tokenHash,
            userId,
            utcNow,
            expiresAtUtc,
            command.RoleId);

        var outboxMessageId = Guid.NewGuid();

        var transactionResult = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                invitationStore.Add(invitation, outboxMessageId, rawEmail, protectedToken, utcNow);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return Result<Guid>.Success(outboxMessageId);
            },
            cancellationToken);

        if (transactionResult.IsFailure)
        {
            return Result<CreateTenantInvitationResult>.Failure(transactionResult.Error);
        }

        // Best effort immediate dispatch outside transaction
        var dispatchedImmediately = false;
        try
        {
            dispatchedImmediately = await emailOutboxProcessor.TryDispatchImmediatelyAsync(
                outboxMessageId,
                rawToken,
                cancellationToken);
        }
        catch
        {
            // Transient error in immediate dispatch is handled via outbox retry
            dispatchedImmediately = false;
        }

        var deliveryStatus = dispatchedImmediately ? "Sent" : "Pending";

        return Result<CreateTenantInvitationResult>.Success(
            new CreateTenantInvitationResult(
                invitation.Id,
                invitation.ExpiresAtUtc,
                deliveryStatus));
    }
}
