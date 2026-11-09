using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.Core.Storage;

public sealed class ProfileStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private List<Profile> _profiles;

    public ProfileStore(AppPaths paths)
    {
        _path = paths.ProfilesFile;
        _profiles = JsonFile.Load(_path, () => new List<Profile>());
        if (!_profiles.Any(p => p.Id == Profile.DefaultId))
        {
            _profiles.Insert(0, Profile.CreateDefault());
            Persist();
        }
    }

    public IReadOnlyList<Profile> All
    {
        get
        {
            lock (_gate)
            {
                return _profiles.Select(Clone).ToList();
            }
        }
    }

    public Profile Get(string? id)
    {
        lock (_gate)
        {
            var match = _profiles.FirstOrDefault(p => p.Id == id);
            return Clone(match ?? _profiles.First(p => p.Id == Profile.DefaultId));
        }
    }

    public Profile Create(string name, string colorHex)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A profile name is required.", nameof(name));
        }

        lock (_gate)
        {
            if (_profiles.Any(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A profile named '{trimmed}' already exists.");
            }

            var profile = new Profile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = trimmed,
                ColorHex = colorHex,
                CreatedUtc = DateTime.UtcNow
            };
            _profiles.Add(profile);
            Persist();
            return Clone(profile);
        }
    }

    public void Rename(string id, string newName)
    {
        var trimmed = (newName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A profile name is required.", nameof(newName));
        }

        lock (_gate)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == id)
                          ?? throw new InvalidOperationException("Profile not found.");
            if (profile.IsDefault)
            {
                throw new InvalidOperationException("The default profile cannot be renamed.");
            }

            if (_profiles.Any(p => p.Id != id && string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A profile named '{trimmed}' already exists.");
            }

            profile.Name = trimmed;
            Persist();
        }
    }

    /// <summary>Removes a profile. Backups are never deleted here; callers reassign them.</summary>
    public void Delete(string id)
    {
        lock (_gate)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == id)
                          ?? throw new InvalidOperationException("Profile not found.");
            if (profile.IsDefault)
            {
                throw new InvalidOperationException("The default profile cannot be deleted.");
            }

            _profiles.Remove(profile);
            Persist();
        }
    }

    private void Persist() => JsonFile.Save(_path, _profiles);

    private static Profile Clone(Profile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        ColorHex = profile.ColorHex,
        CreatedUtc = profile.CreatedUtc
    };
}
