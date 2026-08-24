namespace TeacherOS.Infrastructure.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Brevo";
    public string? BrevoApiKey { get; set; }
    public string FromName { get; set; } = "TeacherOS";
    public string FromAddress { get; set; } = "noreply@teachos.local";
    public string? InvitationBaseUrl { get; set; }
}
