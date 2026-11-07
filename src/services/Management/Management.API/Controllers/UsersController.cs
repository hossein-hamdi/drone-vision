using Management.Application.Abstractions;
using Management.Application.Authorization;
using Management.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.API.Controllers;

[Route("users")]
public sealed class UsersController(IUserService users) : ApiController
{
    /// <summary>List users, their roles, and inherited permissions with filtering and pagination (1–100 items).</summary>
    [HttpGet, Authorize(Policy = Permissions.UsersView)]
    [ProducesResponseType<PagedResult<UserDto>>(200)]
    public async Task<ActionResult<PagedResult<UserDto>>> List([FromQuery] UserFilter filter, CancellationToken ct) =>
        Ok(await users.ListAsync(filter, ct));

    /// <summary>Get a user's public information, roles, and inherited permissions.</summary>
    [HttpGet("{id:int:min(1)}"), Authorize(Policy = Permissions.UsersView)]
    [ProducesResponseType<UserDto>(200)]
    public async Task<ActionResult<UserDto>> Get(int id, CancellationToken ct) => Ok(await users.GetAsync(id, ct));

    /// <summary>Create a user and assign existing roles.</summary>
    /// <remarks>The response includes the distinct permissions inherited from the assigned roles.</remarks>
    [HttpPost, Authorize(Policy = Permissions.UsersCreate)]
    [ProducesResponseType<UserDto>(201)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    /// <summary>Update user details and synchronize roles. Passwords are unchanged.</summary>
    /// <remarks>The response includes current permissions inherited from the resulting roles.</remarks>
    [HttpPut("{id:int:min(1)}"), Authorize(Policy = Permissions.UsersUpdate)]
    [ProducesResponseType<UserDto>(200)]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest request, CancellationToken ct) =>
        Ok(await users.UpdateAsync(id, request, ct));

    /// <summary>Delete a user, their sessions, role assignments, and profile image.</summary>
    [HttpDelete("{id:int:min(1)}"), Authorize(Policy = Permissions.UsersDelete)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return NoContent();
    }
}
