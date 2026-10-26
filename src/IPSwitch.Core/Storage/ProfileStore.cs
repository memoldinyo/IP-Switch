using IPSwitch.Core.Localization;
using System.Text.Json;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;
using IPSwitch.Core.Profiles;
using IPSwitch.Core.Validation;

namespace IPSwitch.Core.Storage;

/// <summary>Dateiformat für profiles.json sowie Import-/Exportdateien.</summary>
public sealed class ProfileDocument
{
    public const string FormatName = "IPSwitch.Profiles";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;

    public int Version { get; set; } = CurrentVersion;

    public List<IpProfile> Profiles { get; set; } = [];
}

/// <summary>
/// Verwaltet die lokal gespeicherten Profile (%LOCALAPPDATA%\IPSwitch\profiles.json).
/// </summary>
public sealed class ProfileStore
{
    private readonly string _path;
    private List<IpProfile> _profiles = [];

    public ProfileStore(string path) => _path = path;

    /// <summary>Wird nach jeder Änderung ausgelöst.</summary>
    public event EventHandler? Changed;

    public string FilePath => _path;

    /// <summary>Kopie der Profile, alphabetisch sortiert.</summary>
    public IReadOnlyList<IpProfile> Profiles =>
        _profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => p.Clone()).ToList();

    /// <summary>
    /// Lädt die Profile. Bei einer beschädigten Datei wird diese umbenannt (nicht gelöscht) und
    /// eine Fehlermeldung zurückgegeben.
    /// </summary>
    public string? Load()
    {
        if (!File.Exists(_path))
        {
            _profiles = [];
            return null;
        }

        try
        {
            var document = JsonFile.Read<ProfileDocument>(_path) ?? new ProfileDocument();
            _profiles = document.Profiles.Where(p => p is not null).ToList();
            return null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var corruptPath = _path + $".defekt-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Copy(_path, corruptPath, overwrite: false);
            }
            catch (Exception copyEx)
            {
                Log.Error("Sicherung der defekten Profildatei fehlgeschlagen", copyEx);
            }

            Log.Error("Profildatei konnte nicht gelesen werden", ex);
            _profiles = [];
            return Loc.T("store.loadError", ex.Message, corruptPath);
        }
    }

    public IpProfile? Get(Guid id) => _profiles.FirstOrDefault(p => p.Id == id)?.Clone();

    /// <summary>Fügt ein neues Profil hinzu oder aktualisiert ein vorhandenes (gleiche Id).</summary>
    public ValidationResult Save(IpProfile profile)
    {
        var normalized = ProfileConverter.Normalize(profile);
        var result = ProfileValidator.Validate(normalized, _profiles);
        if (!result.IsValid)
        {
            return result;
        }

        var index = _profiles.FindIndex(p => p.Id == normalized.Id);
        normalized.ModifiedAt = DateTimeOffset.Now;
        if (index >= 0)
        {
            normalized.CreatedAt = _profiles[index].CreatedAt;
            _profiles[index] = normalized;
        }
        else
        {
            normalized.CreatedAt = normalized.ModifiedAt;
            _profiles.Add(normalized);
        }

        Persist();
        return result;
    }

    public bool Delete(Guid id)
    {
        var removed = _profiles.RemoveAll(p => p.Id == id) > 0;
        if (removed)
        {
            Persist();
        }

        return removed;
    }

    /// <summary>Legt eine Kopie mit eindeutigem Namen an ("Name (Kopie)", "Name (Kopie 2)", ...).</summary>
    public IpProfile Duplicate(Guid id)
    {
        var source = _profiles.FirstOrDefault(p => p.Id == id)
                     ?? throw new InvalidOperationException(Loc.T("store.profileGone"));
        var copy = source.Clone();
        copy.Id = Guid.NewGuid();
        var stem = TrimToLength(source.Name, ProfileValidator.MaxNameLength - 12);
        var copyWord = Loc.T("store.copySuffix");
        copy.Name = MakeUniqueName($"{stem} ({copyWord})", _profiles, i => $"{stem} ({copyWord} {i})");
        copy.CreatedAt = copy.ModifiedAt = DateTimeOffset.Now;
        _profiles.Add(copy);
        Persist();
        return copy.Clone();
    }

    /// <summary>Übernimmt bereits validierte Profile (z. B. aus einem Import).</summary>
    internal void AddRange(IEnumerable<IpProfile> profiles)
    {
        _profiles.AddRange(profiles);
        Persist();
    }

    internal IReadOnlyList<IpProfile> RawProfiles => _profiles;

    /// <summary>
    /// Liefert <paramref name="baseName"/> oder – falls belegt – die erste freie Variante
    /// (Standard: "Name (2)", "Name (3)", ...).
    /// </summary>
    public static string MakeUniqueName(string baseName, IEnumerable<IpProfile> existing, Func<int, string>? variant = null)
    {
        var names = new HashSet<string>(existing.Select(p => p.Name.Trim()), StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(baseName))
        {
            return baseName;
        }

        variant ??= i => $"{baseName} ({i})";
        for (var i = 2; ; i++)
        {
            var candidate = variant(i);
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string TrimToLength(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd();

    private void Persist()
    {
        JsonFile.WriteAtomic(_path, new ProfileDocument { Profiles = _profiles });
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
