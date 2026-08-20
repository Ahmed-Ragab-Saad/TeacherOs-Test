namespace TeacherOS.Application.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_preserves_the_machine_readable_error()
    {
        var error = new Error("Authorization.Forbidden", "Access is denied.", ErrorType.Forbidden);

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal("Authorization.Forbidden", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public void Generic_failure_does_not_expose_a_value()
    {
        var error = new Error("Students.NotFound", "The requested value was not found.", ErrorType.NotFound);
        var result = Result<string>.Failure(error);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
