using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Persistence;

namespace TeacherOS.Infrastructure.Tenancy;

internal sealed class TenantOnboardingStore(ApplicationDbContext dbContext) : ITenantOnboardingStore
{
    public void Add(Tenant tenant, Role ownerRole, TenantMembership membership)
    {
        dbContext.Tenants.Add(tenant);
        dbContext.Roles.Add(ownerRole);
        dbContext.TenantMemberships.Add(membership);
    }
}
