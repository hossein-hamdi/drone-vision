using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Management.API.OpenApi;

public sealed class SecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            if (context.ApiDescription.RelativePath is "auth/login" or "auth/refresh" or "auth/logout")
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-Auth-Transport",
                    In = ParameterLocation.Header,
                    Description = "Omit for browser cookies; native clients set native and send refreshToken in JSON.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-CSRF-TOKEN",
                    In = ParameterLocation.Header,
                    Description = "Browser CSRF request token from GET /auth/csrf. Swagger supplies it automatically.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
            return;
        }
        operation.Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        }];
    }
}
