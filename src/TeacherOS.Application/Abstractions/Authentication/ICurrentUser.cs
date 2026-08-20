namespace TeacherOS.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }
}
