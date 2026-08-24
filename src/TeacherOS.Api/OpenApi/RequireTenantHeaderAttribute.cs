using System;

namespace TeacherOS.Api.OpenApi;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
internal sealed class RequireTenantHeaderAttribute : Attribute
{
}
