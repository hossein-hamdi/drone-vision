using Management.Application.Abstractions;
using Management.Application.Authorization;
using Management.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.API.Controllers;

[Route("roles")]
public sealed class RolesController(IRoleService roles) : ApiController
{
    /// <summary>List roles and their current permissions.</summary>
    [HttpGet, Authorize(Policy = Permissions.RolesView)]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(200)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct) => Ok(await roles.ListAsync(ct));

    /// <summary>Get a role by integer ID.</summary>
    [HttpGet("{id:int:min(1)}"), Authorize(Policy = Permissions.RolesView)]
    [ProducesResponseType<RoleDto>(200)]
    public async Task<ActionResult<RoleDto>> Get(int id, CancellationToken ct) => Ok(await roles.GetAsync(id, ct));

    /// <summary>Create a role with predefined permissions.</summary>
    [HttpPost, Authorize(Policy = Permissions.RolesCreate)]
    [ProducesResponseType<RoleDto>(201)]
    public async Task<ActionResult<RoleDto>> Create(RoleRequest request, CancellationToken ct)
    {
        var role = await roles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
    }

    /// <summary>Replace a role's name and permissions. Bootstrap Admin retains all predefined permissions.</summary>
    [HttpPut("{id:int:min(1)}"), Authorize(Policy = Permissions.RolesUpdate)]
    [ProducesResponseType<RoleDto>(200)]
    public async Task<ActionResult<RoleDto>> Update(int id, RoleRequest request, CancellationToken ct) =>
        Ok(await roles.UpdateAsync(id, request, ct));

    /// <summary>Delete a role and its assignments. Bootstrap Admin is protected.</summary>
    [HttpDelete("{id:int:min(1)}"), Authorize(Policy = Permissions.RolesDelete)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return NoContent();
    }
}
