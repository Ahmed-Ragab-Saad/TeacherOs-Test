using TeacherOS.Application.Abstractions.Tenancy;

namespace TeacherOS.Api.Tenancy;

internal sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public bool IsAvailable => _tenantId.HasValue;

    public Guid TenantId => _tenantId
        ?? throw new InvalidOperationException("No tenant has been established for this request.");

    internal void Establish(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier is required.", nameof(tenantId));
        }

        if (_tenantId.HasValue)
        {
            throw new InvalidOperationException("A tenant has already been established for this request.");
        }

        _tenantId = tenantId;
    }
}
