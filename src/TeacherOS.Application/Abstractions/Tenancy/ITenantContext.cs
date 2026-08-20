namespace TeacherOS.Application.Abstractions.Tenancy;

public interface ITenantContext
{
    bool IsAvailable { get; }

    Guid TenantId { get; }
}
