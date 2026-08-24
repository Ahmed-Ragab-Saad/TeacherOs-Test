using System;

namespace TeacherOS.Application.Abstractions.Email;

public sealed record EmailDispatchResult(
    bool IsSuccess,
    string? ProviderMessageId = null,
    bool IsTransient = false,
    string? ErrorCode = null,
    string? ErrorDescription = null,
    TimeSpan? RetryAfter = null)
{
    public static EmailDispatchResult Success(string? providerMessageId = null) =>
        new(true, ProviderMessageId: providerMessageId);

    public static EmailDispatchResult TransientFailure(
        string errorCode,
        string errorDescription,
        TimeSpan? retryAfter = null) =>
        new(false, IsTransient: true, ErrorCode: errorCode, ErrorDescription: errorDescription, RetryAfter: retryAfter);

    public static EmailDispatchResult PermanentFailure(
        string errorCode,
        string errorDescription) =>
        new(false, IsTransient: false, ErrorCode: errorCode, ErrorDescription: errorDescription);
}
