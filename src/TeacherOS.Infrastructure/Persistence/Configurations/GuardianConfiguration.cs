using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Students;
using TeacherOS.Domain.Tenancy;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("Guardians", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Guardians_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Guardians_TenantId_NotEmpty",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_Guardians_FullName_NotBlank",
                "LEN(LTRIM(RTRIM([FullName]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Guardians_PhoneNumber_NotBlank",
                "LEN(LTRIM(RTRIM([PhoneNumber]))) > 0");
        });

        builder.HasKey(guardian => guardian.Id);

        builder.Property(guardian => guardian.Id)
            .ValueGeneratedNever();

        builder.Property(guardian => guardian.TenantId)
            .IsRequired();

        builder.Property(guardian => guardian.FullName)
            .HasMaxLength(Guardian.MaxFullNameLength)
            .IsRequired();

        builder.Property(guardian => guardian.PhoneNumber)
            .HasMaxLength(Guardian.MaxPhoneNumberLength)
            .IsRequired();

        builder.HasIndex(guardian => guardian.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(guardian => guardian.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
