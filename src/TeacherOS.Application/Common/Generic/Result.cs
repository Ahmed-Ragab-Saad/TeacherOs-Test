namespace TeacherOS.Application.Common;

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("A successful result cannot contain an error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        _value = value;
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public Error Error { get; }

    public static Result<T> Success(T value) => new(value, true, Error.None);

    public static Result<T> Failure(Error error) => new(default, false, error);
}
