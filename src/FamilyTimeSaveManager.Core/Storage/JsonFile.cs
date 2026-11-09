using System.Text.Json;
using System.Text.Json.Serialization;

namespace FamilyTimeSaveManager.Core.Storage;

public static class JsonFile
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Load<T>(string path, Func<T> factory)
    {
        if (!File.Exists(path))
        {
            return factory();
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return factory();
            }

            return JsonSerializer.Deserialize<T>(json, Options) ?? factory();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Quarantine(path);
            return factory();
        }
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, Options);
        FileOps.WriteAllTextAtomic(path, json);
    }

    private static void Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var target = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(path, target, overwrite: false);
        }
        catch (IOException)
        {
            // Reading a fallback value is still correct if the file cannot be moved.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
