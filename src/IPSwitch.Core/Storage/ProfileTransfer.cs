using IPSwitch.Core.Localization;
using System.Text;
using System.Text.Json;
using IPSwitch.Core.Models;
using IPSwitch.Core.Profiles;
using IPSwitch.Core.Validation;

namespace IPSwitch.Core.Storage;

public sealed class ImportReport
{
    public int Imported { get; set; }

    /// <summary>Umbenennungen wegen Namenskonflikten ("Alt → Neu").</summary>
    public List<string> Renamed { get; } = [];

    /// <summary>Übersprungene Profile mit Grund.</summary>
    public List<string> Skipped { get; } = [];

    public string Summary
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine(Loc.T("import.count", Imported));
            if (Renamed.Count > 0)
            {
                sb.AppendLine().AppendLine(Loc.T("import.renamed"));
                Renamed.ForEach(r => sb.AppendLine("• " + r));
            }

            if (Skipped.Count > 0)
            {
                sb.AppendLine().AppendLine(Loc.T("import.skipped"));
                Skipped.ForEach(s => sb.AppendLine("• " + s));
            }

            return sb.ToString().TrimEnd();
        }
    }
}

/// <summary>Import und Export von Profilen als JSON-Datei.</summary>
public static class ProfileTransfer
{
    public const long MaxImportFileSize = 5 * 1024 * 1024;

    public static void Export(string path, IEnumerable<IpProfile> profiles, bool includeAdapterBinding = true)
    {
        var list = profiles.Select(p =>
        {
            var copy = p.Clone();
            if (!includeAdapterBinding)
            {
                copy.Adapter = null;
            }

            return copy;
        }).ToList();
        JsonFile.WriteAtomic(path, new ProfileDocument { Profiles = list });
    }

    /// <summary>Importiert Profile aus einer Datei in den Speicher. Ungültige Einträge werden übersprungen.</summary>
    public static ImportReport Import(string path, ProfileStore store)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException(Loc.T("import.notFound"), path);
        }

        if (info.Length > MaxImportFileSize)
        {
            throw new InvalidDataException(Loc.T("import.tooLarge", MaxImportFileSize / 1024 / 1024));
        }

        var candidates = Parse(File.ReadAllText(path, Encoding.UTF8));
        var report = new ImportReport();
        var accepted = new List<IpProfile>();

        foreach (var candidate in candidates)
        {
            var existing = store.RawProfiles.Concat(accepted).ToList();
            var profile = ProfileConverter.Normalize(candidate);
            var originalName = profile.Name;

            if (profile.Id == Guid.Empty || existing.Any(p => p.Id == profile.Id))
            {
                profile.Id = Guid.NewGuid();
            }

            if (originalName.Length > 0)
            {
                profile.Name = ProfileStore.MakeUniqueName(originalName, existing);
                if (profile.Name != originalName)
                {
                    report.Renamed.Add($"{originalName} → {profile.Name}");
                }
            }

            var validation = ProfileValidator.Validate(profile, existing);
            if (!validation.IsValid)
            {
                var label = originalName.Length > 0 ? originalName : Loc.T("import.noName");
                report.Skipped.Add($"{label}: {string.Join(" ", validation.Errors.Select(e => e.Message))}");
                continue;
            }

            profile.CreatedAt = profile.CreatedAt == default ? DateTimeOffset.Now : profile.CreatedAt;
            profile.ModifiedAt = DateTimeOffset.Now;
            accepted.Add(profile);
        }

        if (accepted.Count > 0)
        {
            store.AddRange(accepted);
        }

        report.Imported = accepted.Count;
        return report;
    }

    /// <summary>Akzeptiert das eigene Dateiformat oder ein reines JSON-Array von Profilen.</summary>
    internal static List<IpProfile> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(Loc.T("import.invalidJson", ex.Message), ex);
        }

        using (document)
        {
            var root = document.RootElement;
            try
            {
                if (root.ValueKind == JsonValueKind.Array)
                {
                    return root.Deserialize<List<IpProfile>>(JsonFile.Options)?.Where(p => p is not null).ToList() ?? [];
                }

                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(Loc.T("import.unknownFormat"));
                }

                var doc = root.Deserialize<ProfileDocument>(JsonFile.Options) ?? new ProfileDocument();
                if (!string.Equals(doc.Format, ProfileDocument.FormatName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(Loc.T("import.wrongFormat", doc.Format, ProfileDocument.FormatName));
                }

                if (doc.Version > ProfileDocument.CurrentVersion)
                {
                    throw new InvalidDataException(Loc.T("import.newerVersion", doc.Version));
                }

                return doc.Profiles.Where(p => p is not null).ToList();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(Loc.T("import.corrupt", ex.Message), ex);
            }
        }
    }
}
