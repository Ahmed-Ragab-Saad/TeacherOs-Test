using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Application.Abstractions.Tenancy;

public interface ITenantOnboardingStore
{
    void Add(Tenant tenant, Role ownerRole, TenantMembership membership);
}
