using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using TeacherOS.Application.Abstractions.Authentication;
using TeacherOS.Application.Abstractions.Authorization;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Application.Abstractions.Invitations;
using TeacherOS.Application.Abstractions.Memberships;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Infrastructure.Authorization;
using TeacherOS.Infrastructure.Configuration;
using TeacherOS.Infrastructure.Email;
using TeacherOS.Infrastructure.Identity;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;

namespace TeacherOS.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"{DatabaseOptions.SectionName}:ConnectionString must be configured.")
            .ValidateOnStart();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;

            options.UseSqlServer(
                databaseOptions.ConnectionString,
                sqlServerOptions =>
                {
                    sqlServerOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    sqlServerOptions.EnableRetryOnFailure();
                });
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services
            .AddDataProtection()
            .SetApplicationName("TeacherOS")
            .PersistKeysToDbContext<ApplicationDbContext>();

        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));

        services.AddHttpClient<ITransactionalEmailSender, BrevoEmailSender>();

        services.AddScoped<IIdentityAuthenticator, IdentityAuthenticator>();
        services.AddScoped<IIdentityUserRegistrar, IdentityUserRegistrar>();
        services.AddScoped<ICurrentSessionReader, CurrentSessionReader>();
        services.AddScoped<ITenantMembershipResolver, TenantMembershipResolver>();
        services.AddScoped<ITenantOnboardingStore, TenantOnboardingStore>();
        services.AddScoped<IIdentityPrincipalFactory, IdentityPrincipalFactory>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddScoped<IInvitationTokenService, InvitationTokenService>();
        services.AddScoped<ITenantInvitationStore, TenantInvitationStore>();
        services.AddScoped<ITenantMembershipManagementStore, TenantMembershipManagementStore>();
        services.AddScoped<IEmailOutboxProcessor, EmailOutboxProcessor>();
        services.AddHostedService<Email.EmailOutboxBackgroundService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.TryAddSingleton(TimeProvider.System);

        services
            .AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

        return services;
    }
}
