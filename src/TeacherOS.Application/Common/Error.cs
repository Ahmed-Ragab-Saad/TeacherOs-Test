namespace TeacherOS.Application.Common;

public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static Error None { get; } = new(string.Empty, string.Empty, ErrorType.None);
}
