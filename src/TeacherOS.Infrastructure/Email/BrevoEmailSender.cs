using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeacherOS.Application.Abstractions.Email;
using TeacherOS.Infrastructure.Configuration;

namespace TeacherOS.Infrastructure.Email;

internal sealed class BrevoEmailSender : ITransactionalEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(
        HttpClient httpClient,
        IOptions<EmailOptions> options,
        ILogger<BrevoEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.brevo.com/");
        }
    }

    public async Task<EmailDispatchResult> SendInvitationEmailAsync(
        InvitationEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.BrevoApiKey))
        {
            _logger.LogWarning("Brevo API key is not configured.");
            return EmailDispatchResult.PermanentFailure("ConfigurationError", "Brevo API key is not configured.");
        }

        var acceptUrl = request.InvitationInspectionUrl;
        if (string.IsNullOrWhiteSpace(acceptUrl) && !string.IsNullOrWhiteSpace(_options.InvitationBaseUrl))
        {
            acceptUrl = $"{_options.InvitationBaseUrl.TrimEnd('/')}#token={Uri.EscapeDataString(request.RawInvitationToken)}";
        }

        var htmlContent = BuildHtmlBody(request, acceptUrl);
        var textContent = BuildTextBody(request, acceptUrl);

        var payload = new BrevoSendEmailPayload
        {
            Sender = new BrevoContact(_options.FromName, _options.FromAddress),
            To = [new BrevoContact(request.RecipientEmail, request.RecipientEmail)],
            Subject = $"Invitation to join {request.TenantDisplayName} on TeacherOS",
            HtmlContent = htmlContent,
            TextContent = textContent,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(payload),
        };

        httpRequest.Headers.Add("api-key", _options.BrevoApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadFromJsonAsync<BrevoSendEmailResponse>(cancellationToken);
                return EmailDispatchResult.Success(responseContent?.MessageId);
            }

            var statusCode = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                return EmailDispatchResult.TransientFailure(
                    "RateLimitExceeded",
                    "Brevo rate limit exceeded.",
                    retryAfter);
            }

            if (statusCode >= 500)
            {
                return EmailDispatchResult.TransientFailure(
                    "ServerError",
                    $"Brevo server error ({statusCode}).");
            }

            return EmailDispatchResult.PermanentFailure(
                "ClientError",
                $"Brevo request rejected ({statusCode}).");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("HTTP request exception while sending email through Brevo: {Message}", ex.Message);
            return EmailDispatchResult.TransientFailure("NetworkError", ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception while sending email through Brevo: {Message}", ex.Message);
            return EmailDispatchResult.TransientFailure("UnexpectedError", ex.Message);
        }
    }

    private static string BuildHtmlBody(InvitationEmailRequest request, string? inspectionUrl)
    {
        var roleText = string.IsNullOrWhiteSpace(request.RoleDisplayName)
            ? string.Empty
            : $"<p><strong>Role:</strong> {WebUtility.HtmlEncode(request.RoleDisplayName)}</p>";

        var actionButton = string.IsNullOrWhiteSpace(inspectionUrl)
            ? string.Empty
            : $@"<div style=""margin: 24px 0;"">
                    <a href=""{WebUtility.HtmlEncode(inspectionUrl)}"" style=""background-color: #2563eb; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Accept Invitation</a>
                 </div>";

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>TeacherOS Invitation</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <h2 style=""color: #1e40af;"">TeacherOS</h2>
    <p>Hello,</p>
    <p>You have been invited to join <strong>{WebUtility.HtmlEncode(request.TenantDisplayName)}</strong> on TeacherOS.</p>
    <p><strong>Invited Email:</strong> {WebUtility.HtmlEncode(request.RecipientEmail)}</p>
    {roleText}
    <p><strong>Expires At:</strong> {request.ExpiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC</p>
    <p>Your invitation token is:</p>
    <div style=""background-color: #f3f4f6; padding: 12px; border-radius: 4px; font-family: monospace; font-size: 14px; word-break: break-all;"">
        {WebUtility.HtmlEncode(request.RawInvitationToken)}
    </div>
    {actionButton}
    <p style=""color: #6b7280; font-size: 12px; margin-top: 32px;"">If you were not expecting this invitation, you can safely ignore this email.</p>
</body>
</html>";
    }

    private static string BuildTextBody(InvitationEmailRequest request, string? inspectionUrl)
    {
        var roleText = string.IsNullOrWhiteSpace(request.RoleDisplayName)
            ? string.Empty
            : $"\nRole: {request.RoleDisplayName}";

        var linkText = string.IsNullOrWhiteSpace(inspectionUrl)
            ? string.Empty
            : $"\n\nAccept link: {inspectionUrl}";

        return $@"TeacherOS

Hello,

You have been invited to join {request.TenantDisplayName} on TeacherOS.

Invited Email: {request.RecipientEmail}{roleText}
Expires At: {request.ExpiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC

Invitation Token:
{request.RawInvitationToken}{linkText}

If you were not expecting this invitation, you can safely ignore this email.";
    }

    private sealed class BrevoSendEmailPayload
    {
        [JsonPropertyName("sender")]
        public BrevoContact Sender { get; set; } = null!;

        [JsonPropertyName("to")]
        public BrevoContact[] To { get; set; } = [];

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("htmlContent")]
        public string HtmlContent { get; set; } = string.Empty;

        [JsonPropertyName("textContent")]
        public string TextContent { get; set; } = string.Empty;
    }

    private sealed class BrevoContact(string name, string email)
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = name;

        [JsonPropertyName("email")]
        public string Email { get; set; } = email;
    }

    private sealed class BrevoSendEmailResponse
    {
        [JsonPropertyName("messageId")]
        public string? MessageId { get; set; }
    }
}
