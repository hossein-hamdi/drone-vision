using Management.Application.Abstractions;
using Management.Application.Authorization;
using Management.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.API.Controllers;

[Route("permissions")]
public sealed class PermissionsController(IRoleService roles) : ApiController
{
    /// <summary>List predefined permissions from the local code catalog.</summary>
    [HttpGet, Authorize(Policy = Permissions.PermissionsView)]
    [ProducesResponseType<IReadOnlyList<PermissionDto>>(200)]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> List(CancellationToken ct) => Ok(await roles.PermissionsAsync(ct));
}
