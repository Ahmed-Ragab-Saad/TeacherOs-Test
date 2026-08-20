using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Common;

namespace TeacherOS.Application.Authentication;

public sealed class GetCurrentSessionHandler(
    ICurrentUser currentUser,
    ICurrentSessionReader currentSessionReader)
{
    public async Task<Result<CurrentSession>> HandleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Result<CurrentSession>.Failure(AuthenticationErrors.SessionUnavailable);
        }

        var session = await currentSessionReader.GetAsync(userId, cancellationToken);

        return session is null
            ? Result<CurrentSession>.Failure(AuthenticationErrors.SessionUnavailable)
            : Result<CurrentSession>.Success(session);
    }
}
