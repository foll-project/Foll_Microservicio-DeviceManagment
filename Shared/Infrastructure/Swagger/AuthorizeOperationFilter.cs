using Microsoft.AspNetCore.Authorization;
using foll_device_managment.Shared.Infrastructure.Security;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace foll_device_managment.Shared.Infrastructure.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var allowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();

        if (allowAnonymous)
        {
            return;
        }

        var hasAuthorize = metadata.OfType<IAuthorizeData>().Any();

        var hasAssignmentKey = metadata.OfType<EmergencyAssignmentKeyAttribute>().Any();

        if (!hasAuthorize && !hasAssignmentKey)
        {
            return;
        }

        operation.Responses ??= [];

        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses.Add("401", new OpenApiResponse
            {
                Description = "Unauthorized"
            });
        }

        if (!operation.Responses.ContainsKey("403"))
        {
            operation.Responses.Add("403", new OpenApiResponse
            {
                Description = "Forbidden"
            });
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = hasAssignmentKey ? "EmergencyAssignmentKey" : "Bearer"
                        }
                    }
                ] = []
            }
        ];
    }
}
