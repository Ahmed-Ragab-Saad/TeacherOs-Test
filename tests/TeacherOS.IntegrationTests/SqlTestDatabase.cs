using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TeacherOS.Application.Abstractions.Tenancy;
using TeacherOS.Infrastructure.Persistence;
using TeacherOS.Infrastructure.Tenancy;

namespace TeacherOS.IntegrationTests;

/// <summary>
/// Provisions a unique, isolated SQL Server database for a single integration test scope.
/// Each instance creates a fresh database with a unique name derived from the CI connection
/// string (via TEACHEROS_TEST_DB_CONNECTION_STRING) or the local-dev fallback, migrates it
/// to the latest schema, and deletes it on dispose.
///
/// Usage — one instance per test method:
/// <code>
///     await using var db = await SqlTestDatabase.CreateAsync();
///     var services = BuildServiceProvider(db.ConnectionString);
///     // run test ...
/// </code>
/// </summary>
internal sealed class SqlTestDatabase : IAsyncDisposable
{
    // Local-dev fallback — NOT used in CI (CI sets TEACHEROS_TEST_DB_CONNECTION_STRING).
    private const string LocalDevBaseConnection =
        "Server=localhost\\MSSQLSERVER01;Database=TeacherOS;Trusted_Connection=true;TrustServerCertificate=true;Encrypt=true;";

    private readonly string _connectionString;

    private SqlTestDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// The fully-qualified connection string pointing to the unique test database.
    /// Pass this value to <c>ConfigurationBuilder</c> as <c>Database:ConnectionString</c>.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Creates the unique database and runs all EF Core migrations synchronously.
    /// Call once per test method, then <c>await using</c> the returned instance.
    /// </summary>
    public static async Task<SqlTestDatabase> CreateAsync()
    {
        var uniqueName = $"TeacherOS_CI_{Guid.NewGuid():N}";
        var connStr = BuildConnectionString(uniqueName);

        var db = new SqlTestDatabase(connStr);
        await db.MigrateAsync();
        return db;
    }

    /// <summary>
    /// Drops the test database. Called automatically by <c>await using</c>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var dbContext = BuildDbContext(_connectionString);
            await dbContext.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Best-effort cleanup — do not mask test failures.
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildConnectionString(string databaseName)
    {
        // Priority 1: CI connection string set by GitHub Actions.
        var ciConn = Environment.GetEnvironmentVariable("TEACHEROS_TEST_DB_CONNECTION_STRING");

        // Priority 2: Secondary env var sometimes used on developer machines.
        if (string.IsNullOrWhiteSpace(ciConn))
        {
            ciConn = Environment.GetEnvironmentVariable("Database__ConnectionString");
        }

        // Priority 3: Hard-coded local-dev fallback (Windows auth, named instance).
        if (string.IsNullOrWhiteSpace(ciConn))
        {
            ciConn = LocalDevBaseConnection;
        }

        // Replace ONLY the database name; keep every other connection property intact
        // (server, credentials, encryption, port, etc.).
        var builder = new SqlConnectionStringBuilder(ciConn)
        {
            InitialCatalog = databaseName,
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Instantiates <see cref="ApplicationDbContext"/> directly using
    /// <see cref="DbContextOptionsBuilder{TContext}"/> and a no-op <see cref="ITenantContext"/>.
    /// Migration does not require an established tenant, so a stub is sufficient.
    /// </summary>
    private static ApplicationDbContext BuildDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        // Migrations and EnsureDeleted do not execute tenant-filtered queries,
        // so a stub ITenantContext (IsAvailable = false) is safe here.
        return new ApplicationDbContext(options, new NoOpTenantContext());
    }

    private async Task MigrateAsync()
    {
        await using var dbContext = BuildDbContext(_connectionString);
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Stub <see cref="ITenantContext"/> used only during migration and cleanup.
    /// Never establishes a tenant — query filters evaluate to false for tenant-owned
    /// entities, which is acceptable because no data is read during migration.
    /// </summary>
    private sealed class NoOpTenantContext : ITenantContext
    {
        public bool IsAvailable => false;
        public Guid TenantId => throw new InvalidOperationException("No tenant established.");
        public void Establish(Guid tenantId) { }
    }
}
