namespace TeacherOS.Api.Observability;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextItemName = "TeacherOS.CorrelationId";

    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext httpContext, CorrelationContext correlationContext)
    {
        var correlationId = GetCorrelationId(httpContext);

        correlationContext.Initialize(correlationId);
        httpContext.Items[HttpContextItemName] = correlationId;
        httpContext.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
        }))
        {
            await next(httpContext);
        }
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        var requestedValues = httpContext.Request.Headers[HeaderName];

        if (requestedValues.Count == 1)
        {
            var requestedValue = requestedValues[0];

            if (IsValid(requestedValue))
            {
                return requestedValue!;
            }
        }

        return Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
    }

    private static bool IsValid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxCorrelationIdLength
            && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
