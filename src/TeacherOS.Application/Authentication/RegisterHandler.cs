using System;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Data;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Application.Common;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Authentication;

public sealed class RegisterHandler(
    IIdentityUserRegistrar userRegistrar,
    ITenantOnboardingStore onboardingStore,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<RegisterResult>> HandleAsync(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = command.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            return Result<RegisterResult>.Failure(AuthenticationErrors.InvalidEmail);
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            return Result<RegisterResult>.Failure(AuthenticationErrors.PasswordRequired);
        }

        var tenantName = command.TenantName?.Trim();
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            return Result<RegisterResult>.Failure(AuthenticationErrors.TenantNameRequired);
        }

        if (tenantName.Length > Tenant.MaxNameLength)
        {
            return Result<RegisterResult>.Failure(AuthenticationErrors.TenantNameTooLong);
        }

        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var identityResult = await userRegistrar.RegisterAsync(email, command.Password, transactionCancellationToken);
                if (identityResult.IsFailure)
                {
                    return Result<RegisterResult>.Failure(identityResult.Error);
                }

                Tenant tenant;
                Role ownerRole;
                TenantMembership membership;

                try
                {
                    tenant = new Tenant(Guid.NewGuid(), tenantName, TenantStatus.Active);
                    ownerRole = new Role(Guid.NewGuid(), tenant.Id, "Owner", Permission.All);
                    membership = new TenantMembership(
                        Guid.NewGuid(),
                        tenant.Id,
                        identityResult.Value.UserId,
                        TenantMembershipStatus.Active,
                        ownerRole.Id);
                }
                catch (ArgumentException ex)
                {
                    return Result<RegisterResult>.Failure(
                        new Error("Authentication.InvalidTenantData", ex.Message, ErrorType.Validation));
                }

                onboardingStore.Add(tenant, ownerRole, membership);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);

                return Result<RegisterResult>.Success(new RegisterResult(
                    identityResult.Value.UserId,
                    identityResult.Value.Email,
                    tenant.Id));
            },
            cancellationToken);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
