using System.Security.Claims;
using TeacherOS.Application.Abstractions.Authentication;

namespace TeacherOS.Api.Authentication;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) && userId != Guid.Empty
                ? userId
                : null;
        }
    }
}
