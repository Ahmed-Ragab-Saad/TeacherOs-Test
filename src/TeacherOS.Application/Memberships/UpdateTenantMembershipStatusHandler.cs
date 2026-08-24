using System;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Memberships;

public sealed class UpdateTenantMembershipStatusHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ITenantMembershipManagementStore membershipStore,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(
        UpdateTenantMembershipStatusCommand command,
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

        if (!Enum.IsDefined(command.NewStatus))
        {
            return Result.Failure(MembershipErrors.InvalidStatus);
        }

        var transactionResult = await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var membership = await membershipStore.GetMembershipAsync(command.MembershipId, command.TenantId, transactionCancellationToken);
                if (membership is null)
                {
                    return Result<bool>.Failure(MembershipErrors.NotFound);
                }

                if (membership.Status == command.NewStatus)
                {
                    return Result<bool>.Failure(MembershipErrors.AlreadyInStatus);
                }

                if (command.NewStatus == TenantMembershipStatus.Suspended)
                {
                    var isMemberOwner = await membershipStore.IsMemberActiveOwnerAsync(
                        command.TenantId,
                        command.MembershipId,
                        transactionCancellationToken);

                    if (isMemberOwner)
                    {
                        var activeOwnersCount = await membershipStore.CountActiveOwnersAsync(
                            command.TenantId,
                            transactionCancellationToken);

                        if (activeOwnersCount <= 1)
                        {
                            return Result<bool>.Failure(MembershipErrors.CannotDisableLastOwner);
                        }
                    }
                }

                membership.UpdateStatus(command.NewStatus);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                return Result<bool>.Success(true);
            },
            cancellationToken);

        return transactionResult.IsSuccess ? Result.Success() : Result.Failure(transactionResult.Error);
    }
}
