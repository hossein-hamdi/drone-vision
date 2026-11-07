using Management.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.API.Controllers;

[Route("uploads/profiles")]
[Tags("Profile")]
[AllowAnonymous]
public sealed class ProfileImagesController(IProfileImageService images) : ApiController
{
    /// <summary>Get a database-stored profile image using its generated URL.</summary>
    [HttpGet("{fileName}")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Get(string fileName, CancellationToken ct)
    {
        var image = await images.GetAsync(fileName, ct);
        return File(image.Content, image.ContentType);
    }
}
