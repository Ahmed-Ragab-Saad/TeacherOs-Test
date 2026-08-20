namespace TeacherOS.Application.Abstractions.Observability;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
