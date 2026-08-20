using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace TeacherOS.Infrastructure.Identity;

internal sealed class IdentityPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory) : IIdentityPrincipalFactory
{
    public async Task<ClaimsPrincipal?> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return null;
        }

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        return principal;
    }
}
