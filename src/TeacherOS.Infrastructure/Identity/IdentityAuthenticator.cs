using Microsoft.AspNetCore.Identity;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Authentication;

namespace TeacherOS.Infrastructure.Identity;

internal sealed class IdentityAuthenticator(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IIdentityAuthenticator
{
    public async Task<IdentityAuthenticationResult?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return null;
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        cancellationToken.ThrowIfCancellationRequested();

        return result.Succeeded
            ? new IdentityAuthenticationResult(user.Id, user.Email ?? email)
            : null;
    }
}
