using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Invitations;

public sealed class AcceptTenantInvitationHandler(
    ICurrentUser currentUser,
    ITenantInvitationStore invitationStore,
    ITenantMembershipManagementStore membershipStore,
    IIdentityUserRegistrar userRegistrar,
    IInvitationTokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<AcceptTenantInvitationResult>> HandleAsync(
        AcceptTenantInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.NotFound);
        }

        var tokenHash = tokenService.HashToken(command.Token.Trim());

        if (currentUser.IsAuthenticated && currentUser.UserId is Guid authenticatedUserId)
        {
            // Authenticated user acceptance path
            return await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    var invitation = await invitationStore.GetByTokenHashAsync(tokenHash, transactionCancellationToken);
                    if (invitation is null)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.NotFound);
                    }

                    if (invitation.IsRevoked)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.Revoked);
                    }

                    if (invitation.IsAccepted)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.AlreadyAccepted);
                    }

                    var utcNow = timeProvider.GetUtcNow();
                    if (invitation.IsExpired(utcNow))
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.Expired);
                    }

                    var currentUserEmail = await membershipStore.GetUserEmailAsync(authenticatedUserId, transactionCancellationToken);
                    if (string.IsNullOrWhiteSpace(currentUserEmail) ||
                        !string.Equals(currentUserEmail.Trim().ToUpperInvariant(), invitation.NormalizedEmail, StringComparison.Ordinal))
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.EmailMismatch);
                    }

                    var alreadyMember = await membershipStore.HasActiveMembershipForUserAsync(
                        invitation.TenantId,
                        authenticatedUserId,
                        transactionCancellationToken);

                    if (alreadyMember)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.MemberAlreadyExists);
                    }

                    var membership = new TenantMembership(
                        Guid.NewGuid(),
                        invitation.TenantId,
                        authenticatedUserId,
                        TenantMembershipStatus.Active,
                        invitation.RoleId);

                    invitation.Accept(utcNow);
                    membershipStore.AddMembership(membership);

                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    return Result<AcceptTenantInvitationResult>.Success(
                        new AcceptTenantInvitationResult(
                            invitation.TenantId,
                            authenticatedUserId,
                            invitation.Email,
                            IsNewUser: false));
                },
                cancellationToken);
        }
        else
        {
            // New user registration path
            var password = command.Password?.Trim();
            if (string.IsNullOrWhiteSpace(password))
            {
                return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.PasswordRequired);
            }

            return await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    var invitation = await invitationStore.GetByTokenHashAsync(tokenHash, transactionCancellationToken);
                    if (invitation is null)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.NotFound);
                    }

                    if (invitation.IsRevoked)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.Revoked);
                    }

                    if (invitation.IsAccepted)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.AlreadyAccepted);
                    }

                    var utcNow = timeProvider.GetUtcNow();
                    if (invitation.IsExpired(utcNow))
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(InvitationErrors.Expired);
                    }

                    var registrationResult = await userRegistrar.RegisterAsync(
                        invitation.Email,
                        password,
                        transactionCancellationToken);

                    if (registrationResult.IsFailure)
                    {
                        return Result<AcceptTenantInvitationResult>.Failure(registrationResult.Error);
                    }

                    var newUserId = registrationResult.Value.UserId;

                    var membership = new TenantMembership(
                        Guid.NewGuid(),
                        invitation.TenantId,
                        newUserId,
                        TenantMembershipStatus.Active,
                        invitation.RoleId);

                    invitation.Accept(utcNow);
                    membershipStore.AddMembership(membership);

                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    return Result<AcceptTenantInvitationResult>.Success(
                        new AcceptTenantInvitationResult(
                            invitation.TenantId,
                            newUserId,
                            invitation.Email,
                            IsNewUser: true));
                },
                cancellationToken);
        }
    }
}
