using Microsoft.AspNetCore.Authorization;

namespace TeacherOS.Api.Authorization;


internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    internal string Permission { get; } = permission;
}
