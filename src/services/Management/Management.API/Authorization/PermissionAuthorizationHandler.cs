using Management.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Management.API.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(IPermissionService permissions, IHttpContextAccessor accessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && int.TryParse(context.User.FindFirst("sub")?.Value, out var id) &&
            await permissions.HasAsync(id, requirement.Permission, accessor.HttpContext?.RequestAborted ?? default))
            context.Succeed(requirement);
    }
}
