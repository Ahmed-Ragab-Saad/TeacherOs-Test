using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System;
using System.Linq.Expressions;
using System.Reflection;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Domain.Authorization;
using TeacherOS.Domain.Common;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Identity;
using TeacherOS.Infrastructure.Tenancy;

namespace TeacherOS.Infrastructure.Persistence;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
    : IdentityUserContext<ApplicationUser, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    internal DbSet<Tenant> Tenants => Set<Tenant>();
    internal DbSet<Role> Roles => Set<Role>();
    internal DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplyTenantIsolationFilters(modelBuilder);
    }

    private void ApplyTenantIsolationFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwnedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(ApplicationDbContext)
                .GetMethod(nameof(BuildFailClosedTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            var filter = (LambdaExpression)method.Invoke(this, null)!;
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    private LambdaExpression BuildFailClosedTenantFilter<TEntity>()
        where TEntity : class, ITenantOwnedEntity
    {
        Expression<Func<TEntity, bool>> filter =
            entity => tenantContext.IsAvailable && entity.TenantId == tenantContext.TenantId;
        return filter;
    }
}
