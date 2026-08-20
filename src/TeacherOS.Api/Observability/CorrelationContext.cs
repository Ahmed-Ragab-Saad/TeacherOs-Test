using TeacherOS.Application.Abstractions.Observability;

namespace TeacherOS.Api.Observability;

internal sealed class CorrelationContext : ICorrelationContext
{
    private string? _correlationId;

    public string CorrelationId => _correlationId
        ?? throw new InvalidOperationException("The correlation context has not been initialized.");

    public void Initialize(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (_correlationId is not null)
        {
            throw new InvalidOperationException("The correlation context is already initialized.");
        }

        _correlationId = correlationId;
    }
}
