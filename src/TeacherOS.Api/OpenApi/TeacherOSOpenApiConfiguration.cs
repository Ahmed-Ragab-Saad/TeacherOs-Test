using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using TeacherOS.Api.Authentication;
using TeacherOS.Api.Tenancy;

namespace TeacherOS.Api.OpenApi;

internal static class TeacherOSOpenApiConfiguration
{
    internal const string AntiforgeryHeaderName = "X-CSRF-TOKEN";
    internal const string AntiforgeryHeaderDescription =
        "Antiforgery token returned by GET /api/auth/antiforgery. Must be sent together with the associated antiforgery cookie.";

    internal const string TenantHeaderDescription =
        "Selected tenant identifier. Must match the tenant context authorized for the current authenticated user.";

    internal const string CookieSchemeName = "CookieAuth";
    internal const string AuthCookieName = "__Host-TeacherOS.Auth";

    internal static void ConfigureOpenApi(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            if (!document.Components.SecuritySchemes.ContainsKey(CookieSchemeName))
            {
                document.Components.SecuritySchemes[CookieSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Cookie,
                    Name = AuthCookieName,
                    Description = "Session cookie issued upon successful login.",
                };
            }

            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;

            var requiresAntiforgery = endpointMetadata.OfType<RequireAntiforgeryTokenAttribute>().Any() ||
                                      endpointMetadata.Any(m => m is AntiforgeryEndpointFilter || m.GetType().Name.Contains("Antiforgery", StringComparison.OrdinalIgnoreCase));

            if (requiresAntiforgery)
            {
                UpsertHeaderParameter(
                    operation,
                    AntiforgeryHeaderName,
                    AntiforgeryHeaderDescription,
                    schemaType: JsonSchemaType.String,
                    schemaFormat: null,
                    required: true);
            }

            var requiresTenant = endpointMetadata.OfType<RequireTenantHeaderAttribute>().Any();

            if (requiresTenant)
            {
                UpsertHeaderParameter(
                    operation,
                    TenantContextMiddleware.TenantHeaderName,
                    TenantHeaderDescription,
                    schemaType: JsonSchemaType.String,
                    schemaFormat: "uuid",
                    required: true);
            }

            var hasAuthorize = endpointMetadata.OfType<IAuthorizeData>().Any();
            var allowAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();

            if (hasAuthorize && !allowAnonymous)
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();
                var schemeRef = new OpenApiSecuritySchemeReference(CookieSchemeName, null, null);
                if (!operation.Security.Any(s => s.Keys.Any(k => string.Equals(k.Reference?.Id, CookieSchemeName, StringComparison.OrdinalIgnoreCase))))
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [schemeRef] = new List<string>(),
                    });
                }
            }

            DeduplicateHeaderParameters(operation);

            return Task.CompletedTask;
        });
    }

    private static void UpsertHeaderParameter(
        OpenApiOperation operation,
        string name,
        string description,
        JsonSchemaType schemaType,
        string? schemaFormat,
        bool required)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();

        var existing = operation.Parameters.FirstOrDefault(p =>
            p is OpenApiParameter op &&
            op.In == ParameterLocation.Header &&
            string.Equals(op.Name, name, StringComparison.OrdinalIgnoreCase)) as OpenApiParameter;

        if (existing is not null)
        {
            existing.Name = name;
            existing.Required = required;
            existing.Description = description;
            existing.Schema = new OpenApiSchema
            {
                Type = schemaType,
                Format = schemaFormat,
            };
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description,
            Schema = new OpenApiSchema
            {
                Type = schemaType,
                Format = schemaFormat,
            },
        });
    }

    private static void DeduplicateHeaderParameters(OpenApiOperation operation)
    {
        if (operation.Parameters == null || operation.Parameters.Count <= 1)
        {
            return;
        }

        var uniqueHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filteredParameters = new List<IOpenApiParameter>();

        foreach (var parameter in operation.Parameters)
        {
            if (parameter is OpenApiParameter headerParam && headerParam.In == ParameterLocation.Header)
            {
                if (!string.IsNullOrEmpty(headerParam.Name) && uniqueHeaders.Add(headerParam.Name))
                {
                    filteredParameters.Add(parameter);
                }
            }
            else
            {
                filteredParameters.Add(parameter);
            }
        }

        operation.Parameters = filteredParameters;
    }
}
