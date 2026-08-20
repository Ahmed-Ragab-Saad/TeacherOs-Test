using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Infrastructure.Identity;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AspNetUsers_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
        });

        builder.Property(user => user.Id)
            .ValueGeneratedNever();
    }
}
