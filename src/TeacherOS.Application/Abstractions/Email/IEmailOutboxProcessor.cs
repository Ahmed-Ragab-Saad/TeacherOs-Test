using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeacherOS.Application.Abstractions.Email;

public interface IEmailOutboxProcessor
{
    Task<bool> TryDispatchImmediatelyAsync(
        Guid outboxMessageId,
        string rawToken,
        CancellationToken cancellationToken = default);

    Task<int> ProcessPendingOutboxMessagesAsync(
        CancellationToken cancellationToken = default);
}
