using System.Linq;
using Microsoft.AspNetCore.Identity;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Authentication;
using TeacherOS.Application.Common;

namespace TeacherOS.Infrastructure.Identity;

internal sealed class IdentityUserRegistrar(UserManager<ApplicationUser> userManager) : IIdentityUserRegistrar
{
    public async Task<Result<IdentityRegistrationResult>> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return Result<IdentityRegistrationResult>.Failure(AuthenticationErrors.DuplicateEmail);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
        };

        var result = await userManager.CreateAsync(user, password);
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.Succeeded)
        {
            var isDuplicate = result.Errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail");

            if (isDuplicate)
            {
                return Result<IdentityRegistrationResult>.Failure(AuthenticationErrors.DuplicateEmail);
            }

            var primaryError = result.Errors.FirstOrDefault();
            var description = primaryError?.Description ?? "Failed to create user.";

            return Result<IdentityRegistrationResult>.Failure(
                new Error("Authentication.InvalidPassword", description, ErrorType.Validation));
        }

        return Result<IdentityRegistrationResult>.Success(
            new IdentityRegistrationResult(user.Id, user.Email ?? email));
    }
}
