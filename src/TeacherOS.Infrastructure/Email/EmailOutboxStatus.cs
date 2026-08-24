namespace TeacherOS.Infrastructure.Email;

public enum EmailOutboxStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Failed = 4,
}
