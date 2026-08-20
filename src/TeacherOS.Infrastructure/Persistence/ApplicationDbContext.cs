namespace TeacherOS.Infrastructure.Persistence;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
}
