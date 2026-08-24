using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TeacherOS.Api.Authentication;

namespace TeacherOS.Api.OpenApi;

internal static class OpenApiEndpointExtensions
{
    internal static RouteHandlerBuilder RequireAntiforgeryToken(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new RequireAntiforgeryTokenAttribute());
        builder.AddEndpointFilter<AntiforgeryEndpointFilter>();
        return builder;
    }

    internal static RouteGroupBuilder RequireAntiforgeryToken(this RouteGroupBuilder builder)
    {
        builder.WithMetadata(new RequireAntiforgeryTokenAttribute());
        builder.AddEndpointFilter<AntiforgeryEndpointFilter>();
        return builder;
    }

    internal static TBuilder RequireTenantContext<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new RequireTenantHeaderAttribute());
        return builder;
    }

    internal static TBuilder RequireTenantHeader<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireTenantContext();
    }
}
