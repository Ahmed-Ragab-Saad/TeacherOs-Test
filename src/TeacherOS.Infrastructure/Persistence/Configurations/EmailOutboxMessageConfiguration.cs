using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeacherOS.Domain.Tenancy;
using TeacherOS.Infrastructure.Email;

namespace TeacherOS.Infrastructure.Persistence.Configurations;

internal sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_EmailOutboxMessages_Id_NotEmpty",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_EmailOutboxMessages_TenantInvitationId_NotEmpty",
                "[TenantInvitationId] <> '00000000-0000-0000-0000-000000000000'");
            tableBuilder.HasCheckConstraint(
                "CK_EmailOutboxMessages_RecipientEmail_NotBlank",
                "LEN(LTRIM(RTRIM([RecipientEmail]))) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_EmailOutboxMessages_Status_Valid",
                "[Status] IN (1, 2, 3, 4)");
        });

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.TenantInvitationId)
            .IsRequired();

        builder.Property(message => message.RecipientEmail)
            .HasMaxLength(EmailOutboxMessage.MaxRecipientEmailLength)
            .IsRequired();

        builder.Property(message => message.ProtectedInvitationToken);

        builder.Property(message => message.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(message => message.AttemptCount)
            .IsRequired();

        builder.Property(message => message.MaxAttempts)
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .IsRequired();

        builder.Property(message => message.NextAttemptAtUtc)
            .IsRequired();

        builder.Property(message => message.LastAttemptAtUtc);
        builder.Property(message => message.SentAtUtc);

        builder.Property(message => message.ProviderMessageId)
            .HasMaxLength(EmailOutboxMessage.MaxProviderMessageIdLength);

        builder.Property(message => message.LastErrorCode)
            .HasMaxLength(EmailOutboxMessage.MaxLastErrorCodeLength);

        builder.Property(message => message.LastErrorDescription)
            .HasMaxLength(EmailOutboxMessage.MaxLastErrorDescriptionLength);

        builder.Property(message => message.RowVersion)
            .IsRowVersion();

        builder.HasIndex(message => new { message.Status, message.NextAttemptAtUtc })
            .HasDatabaseName("IX_EmailOutboxMessages_Status_NextAttemptAtUtc");

        builder.HasIndex(message => message.TenantInvitationId)
            .HasDatabaseName("IX_EmailOutboxMessages_TenantInvitationId");

        builder.HasOne<TenantInvitation>()
            .WithMany()
            .HasForeignKey(message => message.TenantInvitationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
