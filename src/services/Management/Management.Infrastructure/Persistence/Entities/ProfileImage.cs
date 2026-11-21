namespace Management.Infrastructure.Persistence.Entities;

public sealed class ProfileImage : ITimestampedEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
