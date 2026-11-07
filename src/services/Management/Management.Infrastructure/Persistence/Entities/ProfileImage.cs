namespace Management.Infrastructure.Persistence.Entities;

public sealed class ProfileImage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
