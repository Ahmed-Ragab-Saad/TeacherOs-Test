using System;
using System.Net.Mail;

namespace TeacherOS.Application.Common;

public static class EmailValidator
{
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
