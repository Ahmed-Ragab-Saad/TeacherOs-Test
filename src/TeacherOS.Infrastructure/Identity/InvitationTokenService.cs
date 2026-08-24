using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using TeacherOS.Application.Abstractions.Invitations;

namespace TeacherOS.Infrastructure.Identity;

internal sealed class InvitationTokenService(IDataProtectionProvider dataProtectionProvider) : IInvitationTokenService
{
    private const string DataProtectionPurpose = "TeacherOS.TenantInvitation.EmailOutbox.v1";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Raw token cannot be null or whitespace.", nameof(rawToken));
        }

        var bytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public string ProtectToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Raw token cannot be null or whitespace.", nameof(rawToken));
        }

        return _protector.Protect(rawToken.Trim());
    }

    public string UnprotectToken(string protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
        {
            throw new ArgumentException("Protected token cannot be null or whitespace.", nameof(protectedToken));
        }

        return _protector.Unprotect(protectedToken.Trim());
    }
}
