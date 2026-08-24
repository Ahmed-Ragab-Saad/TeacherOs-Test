using Microsoft.AspNetCore.Builder;

namespace TeacherOS.Api.Authorization;

internal static class PermissionEndpointExtensions
{
    internal static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization($"Permission:{permission}");
    }
}
