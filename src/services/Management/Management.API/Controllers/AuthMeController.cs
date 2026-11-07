using Management.Application.Abstractions;
using Management.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Management.API.Controllers;

[Route("auth/me")]
[Tags("Auth")]
public sealed class AuthMeController(IUserService users) : ApiController
{
    /// <summary>Get the authenticated user's identity, roles, and current permissions.</summary>
    [HttpGet]
    [ProducesResponseType<AuthenticatedUserDto>(200)]
    public async Task<ActionResult<AuthenticatedUserDto>> Me(CancellationToken ct) =>
        Ok(await users.GetAuthenticationAsync(CurrentUserId, ct));
}
