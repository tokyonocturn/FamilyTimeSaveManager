namespace FamilyTimeSaveManager.Core.Models;

public sealed class Profile
{
    public const string DefaultId = "default";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#5B8DFF";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsDefault => Id == DefaultId;

    public static Profile CreateDefault() => new()
    {
        Id = DefaultId,
        Name = "Default",
        ColorHex = "#5B8DFF",
        CreatedUtc = DateTime.UtcNow
    };
}
