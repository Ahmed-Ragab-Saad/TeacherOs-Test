using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeacherOS.Domain.Students;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;
using Xunit;

namespace TeacherOS.IntegrationTests;

public sealed class StudentTenantIsolationTests
{
    private const string DefaultConnectionString =
        "Server=.;Database=TeacherOS_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly object DatabaseInitLock = new();
    private static bool _databaseInitialized;

    [Fact]
    public async Task Students_query_is_isolated_by_tenant_and_rejects_queries_without_a_tenant()
    {
        var connectionString = ResolveConnectionString();
        EnsureDatabaseMigrated(connectionString);

        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        Guid studentAId;
        Guid studentBId;

        // --- Seed: two tenants, each with one Student. Inserts are unaffected by the
        // query filter (EF Core global query filters apply to reads, not writes), so a
        // single write context with no tenant established is fine here.
        await using (var writeContext = CreateDbContext(connectionString, new TenantContext()))
        {
            var tenantA = new Tenant(tenantAId, $"Isolation Test Tenant A {tenantAId:N}", TenantStatus.Active);
            var tenantB = new Tenant(tenantBId, $"Isolation Test Tenant B {tenantBId:N}", TenantStatus.Active);
            writeContext.Tenants.AddRange(tenantA, tenantB);

            var branchA = new Branch(Guid.NewGuid(), tenantAId, "Main Branch A");
            var branchB = new Branch(Guid.NewGuid(), tenantBId, "Main Branch B");
            writeContext.Branches.AddRange(branchA, branchB);

            var gradeA = new GradeLevel(Guid.NewGuid(), tenantAId, "Grade A", 1);
            var gradeB = new GradeLevel(Guid.NewGuid(), tenantBId, "Grade B", 1);
            writeContext.GradeLevels.AddRange(gradeA, gradeB);

            var studentA = new Student(
                Guid.NewGuid(),
                tenantAId,
                branchA.Id,
                gradeA.Id,
                studentCode: $"A-{Guid.NewGuid():N}"[..10],
                fullName: "Isolation Test Student A",
                nationalId: Guid.NewGuid().ToString("N")[..14],
                enrollmentDate: DateOnly.FromDateTime(DateTime.UtcNow));

            var studentB = new Student(
                Guid.NewGuid(),
                tenantBId,
                branchB.Id,
                gradeB.Id,
                studentCode: $"B-{Guid.NewGuid():N}"[..10],
                fullName: "Isolation Test Student B",
                nationalId: Guid.NewGuid().ToString("N")[..14],
                enrollmentDate: DateOnly.FromDateTime(DateTime.UtcNow));

            writeContext.Students.AddRange(studentA, studentB);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            studentAId = studentA.Id;
            studentBId = studentB.Id;
        }

        // --- Verify: tenant A only ever sees its own student.
        await using (var tenantAContext = CreateDbContext(connectionString, EstablishedTenantContext(tenantAId)))
        {
            var visibleToTenantA = await tenantAContext.Students.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(visibleToTenantA);
            Assert.Equal(studentAId, visibleToTenantA[0].Id);
            Assert.DoesNotContain(visibleToTenantA, student => student.Id == studentBId);
        }

        // --- Verify: tenant B only ever sees its own student (the reverse leak check).
        await using (var tenantBContext = CreateDbContext(connectionString, EstablishedTenantContext(tenantBId)))
        {
            var visibleToTenantB = await tenantBContext.Students.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(visibleToTenantB);
            Assert.Equal(studentBId, visibleToTenantB[0].Id);
            Assert.DoesNotContain(visibleToTenantB, student => student.Id == studentAId);
        }

        // --- Verify: fail-closed. No tenant established at all -> zero rows, never a fallback to "all rows".
        await using (var noTenantContext = CreateDbContext(connectionString, new TenantContext()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await noTenantContext.Students
                    .AsNoTracking()
                    .Where(student => student.Id == studentAId || student.Id == studentBId)
                    .ToListAsync(TestContext.Current.CancellationToken);
            });
        }
    }

    private static TenantContext EstablishedTenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Establish(tenantId);
        return tenantContext;
    }

    private static ApplicationDbContext CreateDbContext(string connectionString, TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    private static string ResolveConnectionString()
    {
        var testEnvConn = Environment.GetEnvironmentVariable("TEACHEROS_TEST_DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(testEnvConn))
        {
            return testEnvConn;
        }

        var databaseEnvConn = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (!string.IsNullOrWhiteSpace(databaseEnvConn))
        {
            return databaseEnvConn;
        }

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddUserSecrets<TeacherOSApiFactory>(optional: true);
        var config = configBuilder.Build();
        var userSecretsConn = config.GetSection("Database:ConnectionString").Value;

        return !string.IsNullOrWhiteSpace(userSecretsConn) ? userSecretsConn : DefaultConnectionString;
    }

    private static void EnsureDatabaseMigrated(string connectionString)
    {
        if (_databaseInitialized)
        {
            return;
        }

        lock (DatabaseInitLock)
        {
            if (_databaseInitialized)
            {
                return;
            }

            using var dbContext = CreateDbContext(connectionString, new TenantContext());
            dbContext.Database.Migrate();
            _databaseInitialized = true;
        }
    }
}
