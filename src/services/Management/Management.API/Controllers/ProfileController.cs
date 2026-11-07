using System.ComponentModel.DataAnnotations;
using Management.Application.Abstractions;
using Management.Application.Authorization;
using Management.Application.DTOs;
using Management.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.API.Controllers;

public sealed class ProfileController(IUserService users, IProfileImageService images) : ApiController
{
    /// <summary>Get the current user's public profile and roles.</summary>
    [HttpGet("profile")]
    [ProducesResponseType<ProfileDto>(200)]
    public async Task<ActionResult<ProfileDto>> Profile(CancellationToken ct) => Ok(await users.ProfileAsync(CurrentUserId, ct));

    /// <summary>Upload a JPEG, PNG, or WebP profile image (maximum 5 MiB).</summary>
    [HttpPost("profile-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileImageStore.MaximumBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileImageStore.MaximumBytes + 65536)]
    [ProducesResponseType<ImageResponse>(200)]
    public Task<ActionResult<ImageResponse>> Upload([FromForm] ImageUpload request, CancellationToken ct) => UploadFor(CurrentUserId, request, ct);

    /// <summary>Delete the current user's profile image.</summary>
    [HttpDelete("profile-image")]
    [ProducesResponseType(204)]
    public Task<IActionResult> Delete(CancellationToken ct) => DeleteFor(CurrentUserId, ct);

    /// <summary>Upload another user's profile image. Requires Users.Update.</summary>
    [HttpPost("users/{id:int:min(1)}/profile-image"), Authorize(Policy = Permissions.UsersUpdate)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileImageStore.MaximumBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileImageStore.MaximumBytes + 65536)]
    [ProducesResponseType<ImageResponse>(200)]
    public Task<ActionResult<ImageResponse>> UploadUser(int id, [FromForm] ImageUpload request, CancellationToken ct) => UploadFor(id, request, ct);

    /// <summary>Delete another user's profile image. Requires Users.Update.</summary>
    [HttpDelete("users/{id:int:min(1)}/profile-image"), Authorize(Policy = Permissions.UsersUpdate)]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteUser(int id, CancellationToken ct) => DeleteFor(id, ct);

    private async Task<ActionResult<ImageResponse>> UploadFor(int id, ImageUpload request, CancellationToken ct)
    {
        await using var stream = request.Image.OpenReadStream();
        return Ok(new ImageResponse(await images.UploadAsync(id, stream, request.Image.Length, ct)));
    }

    private async Task<IActionResult> DeleteFor(int id, CancellationToken ct)
    {
        await images.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class ImageUpload { [Required] public IFormFile Image { get; init; } = null!; }
    public sealed record ImageResponse(string ProfileImageUrl);
}
