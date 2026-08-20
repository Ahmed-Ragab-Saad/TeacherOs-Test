using System.Security.Claims;

namespace TeacherOS.Infrastructure.Identity;

public interface IIdentityPrincipalFactory
{
    Task<ClaimsPrincipal?> CreateAsync(Guid userId, CancellationToken cancellationToken);
}
