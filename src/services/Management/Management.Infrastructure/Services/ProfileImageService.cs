using Microsoft.Extensions.Configuration;
using Management.Application.Abstractions;
using Management.Application.Common;
using Management.Application.DTOs;
using Management.Infrastructure.Persistence;
using Management.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services;

public sealed class ProfileImageService(ProfileImageStore store, ManagementDbContext db, IConfiguration configuration) : IProfileImageService
{
    public async Task<ProfileImageData> GetAsync(string fileName, CancellationToken ct)
    {
        var url = ProfileImageStore.UrlPrefix + fileName;
        return await db.ProfileImages.AsNoTracking().Where(image => image.Url == url)
            .Select(image => new ProfileImageData(image.Content, image.ContentType))
            .SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Profile image");
    }

    public async Task<string> UploadAsync(int userId, Stream image, long length, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) throw ApiException.NotFound("User");
        var stored = await db.ProfileImages.SingleOrDefaultAsync(storedImage => storedImage.UserId == userId, ct);
        var content = await store.ReadAsync(image, length, ct);
        if (stored is null)
        {
            stored = new ProfileImage { UserId = userId };
            db.ProfileImages.Add(stored);
        }
        stored.Url = ProfileImageStore.CreateUrl(content.ContentType);
        stored.Content = content.Content;
        stored.ContentType = content.ContentType;
        await db.SaveChangesAsync(ct);
        return (configuration["Gateway:PathBase"]?.TrimEnd('/') ?? string.Empty) + stored.Url;
    }

    public async Task DeleteAsync(int userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) throw ApiException.NotFound("User");
        var stored = await db.ProfileImages.SingleOrDefaultAsync(storedImage => storedImage.UserId == userId, ct);
        if (stored is null) return;
        db.ProfileImages.Remove(stored);
        await db.SaveChangesAsync(ct);
    }
}
