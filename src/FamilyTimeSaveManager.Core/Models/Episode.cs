namespace FamilyTimeSaveManager.Core.Models;

public enum Episode
{
    Unknown = 0,
    Episode1 = 1,
    Episode2 = 2,
    Episode3 = 3
}

public enum GraphicsApi
{
    Unknown = 0,
    Dx11 = 11,
    Dx12 = 12
}

public static class EpisodeExtensions
{
    public static string ToDisplayName(this Episode episode) => episode switch
    {
        Episode.Episode1 => "Episode 1",
        Episode.Episode2 => "Episode 2",
        Episode.Episode3 => "Episode 3",
        _ => "Unknown episode"
    };

    public static string ToShortLabel(this Episode episode) => episode switch
    {
        Episode.Episode1 => "EP1",
        Episode.Episode2 => "EP2",
        Episode.Episode3 => "EP3",
        _ => "EP?"
    };

    public static Episode FromNumber(int? number) =>
        number is >= 1 and <= 3 ? (Episode)number.Value : Episode.Unknown;

    public static string ToDisplayName(this GraphicsApi api) => api switch
    {
        GraphicsApi.Dx11 => "Direct3D 11",
        GraphicsApi.Dx12 => "Direct3D 12",
        _ => "Unknown renderer"
    };

    public static string ToShortLabel(this GraphicsApi api) => api switch
    {
        GraphicsApi.Dx11 => "DX11",
        GraphicsApi.Dx12 => "DX12",
        _ => "DX?"
    };
}
