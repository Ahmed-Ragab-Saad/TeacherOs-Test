namespace TeacherOS.Api.Authentication;

internal static class AuthenticationConstants
{
    internal const string CookieScheme = "TeacherOS.Application";
    internal const string LoginRateLimitPolicy = "authentication-login";
    internal const string RegisterRateLimitPolicy = "authentication-register";
    internal const string InvitationCreateRateLimitPolicy = "invitation-create";
    internal const string InvitationInspectRateLimitPolicy = "invitation-inspect";
    internal const string InvitationAcceptRateLimitPolicy = "invitation-accept";
}
