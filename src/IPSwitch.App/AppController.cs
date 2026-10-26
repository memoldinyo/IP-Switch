using IPSwitch.Core.Localization;
using IPSwitch.App.Elevation;
using IPSwitch.App.UI;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Profiles;
using IPSwitch.Core.Storage;
using IPSwitch.Core.Validation;

namespace IPSwitch.App;

/// <summary>
/// Gemeinsame Logik für Tray-Menü und Fenster: Zugriff auf Profile, Adapter, Sicherungen und das Anwenden.
/// </summary>
internal sealed class AppController
{
    public AppController(AppPaths paths)
    {
        Paths = paths;
        Settings = AppSettings.Load(paths.SettingsFile);
        Loc.SetLanguage(Settings.Language);
        Profiles = new ProfileStore(paths.ProfilesFile);
        LoadError = Profiles.Load();
        Backups = new BackupStore(paths.BackupDirectory);
        Adapters = new AdapterService();
        Coordinator = new ApplyCoordinator(paths);
        Coordinator.CleanupRuntimeDirectory();
    }

    /// <summary>Netzwerkkonfiguration hat sich geändert (Systemereignis oder eigene Änderung).</summary>
    public event EventHandler? AdaptersChanged;

    /// <summary>Beginn/Ende eines Anwendevorgangs.</summary>
    public event EventHandler? BusyChanged;

    public AppPaths Paths { get; }

    public AppSettings Settings { get; }

    public ProfileStore Profiles { get; }

    public BackupStore Backups { get; }

    public AdapterService Adapters { get; }

    public ApplyCoordinator Coordinator { get; }

    /// <summary>Fehlermeldung beim Laden der Profile (beschädigte Datei) oder null.</summary>
    public string? LoadError { get; private set; }

    /// <summary>Liefert die Lade-Fehlermeldung genau einmal (damit sie nicht bei jedem Fensteröffnen erscheint).</summary>
    public string? ConsumeLoadError()
    {
        var error = LoadError;
        LoadError = null;
        return error;
    }

    public bool IsBusy => Coordinator.IsBusy;

    /// <summary>Wird vom Tray-Kontext gesetzt, um Benachrichtigungen anzuzeigen.</summary>
    public Action<string, ToolTipIcon>? Notify { get; set; }

    public IReadOnlyList<AdapterInfo> GetAdapters() => Adapters.GetAdapters(Settings.ShowAllAdapters);

    public void RaiseAdaptersChanged() => AdaptersChanged?.Invoke(this, EventArgs.Empty);

    public void SaveSettings() => Settings.Save(Paths.SettingsFile);

    public async Task<bool> ApplyProfileAsync(IpProfile profile, AdapterInfo adapter, IWin32Window? owner)
    {
        AdapterConfiguration target;
        try
        {
            target = ProfileConverter.ToConfiguration(profile);
        }
        catch (InvalidOperationException ex)
        {
            Dialogs.Error(owner, Loc.T("apply.profileInvalid", profile.Name), ex.Message);
            return false;
        }

        return await ApplyAsync(adapter, target, Loc.T("label.profile", profile.Name), owner);
    }

    /// <summary>Ordnet ein Profil einem Adapter zu (null = Zuordnung aufheben).</summary>
    /// <returns>Fehlermeldung oder null bei Erfolg.</returns>
    public string? AssignProfile(Guid profileId, AdapterInfo? adapter)
    {
        var profile = Profiles.Get(profileId);
        if (profile is null)
        {
            return Loc.T("store.profileGone");
        }

        profile.Adapter = adapter is null ? null : AdapterBinding.FromAdapter(adapter);
        try
        {
            var result = Profiles.Save(profile);
            return result.IsValid ? null : result.Summary;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    public Task<bool> EnableDhcpAsync(AdapterInfo adapter, IWin32Window? owner) =>
        ApplyAsync(adapter, ProfileConverter.DhcpConfiguration(), Loc.T("action.enableDhcp"), owner);

    public Task<bool> RestoreBackupAsync(ConfigurationBackup backup, AdapterInfo adapter, IWin32Window? owner) =>
        ApplyAsync(adapter, backup.Configuration, Loc.T("label.restoreBackup", backup.CreatedAt.LocalDateTime.ToString("g")), owner);

    /// <summary>
    /// Öffnet den Profileditor. Bei "Speichern und anwenden" wird das Profil anschließend direkt angewendet.
    /// </summary>
    public async Task<IpProfile?> EditProfileAsync(IpProfile profile, bool isNew, IWin32Window? owner)
    {
        var adapters = Adapters.GetAdapters(includeSecondary: true);
        using var editor = new ProfileEditorForm(this, profile, isNew, adapters);
        if (Dialogs.ShowModal(editor, owner) != DialogResult.OK || editor.SavedProfile is null)
        {
            return null;
        }

        var saved = editor.SavedProfile;
        if (editor.ApplyRequested)
        {
            var adapter = AdapterService.Match(Adapters.GetAdapters(includeSecondary: true), saved.Adapter);
            if (adapter is null)
            {
                Dialogs.Error(owner, Loc.T("editor.savedAdapterMissing"));
            }
            else
            {
                await ApplyProfileAsync(saved, adapter, Dialogs.Alive(owner));
            }
        }

        return saved;
    }

    /// <summary>Legt ein neues Profil aus der aktuellen Konfiguration eines Adapters an (Editor wird geöffnet).</summary>
    public Task<IpProfile?> CaptureAdapterAsync(AdapterInfo adapter, IWin32Window? owner)
    {
        var template = ProfileConverter.FromAdapter(adapter);
        template.Name = ProfileStore.MakeUniqueName(Loc.T("capture.name", adapter.Name), Profiles.Profiles);
        template.Description = Loc.T("capture.description", DateTime.Now.ToString("g"), adapter.Name);
        return EditProfileAsync(template, isNew: true, owner);
    }

    private async Task<bool> ApplyAsync(AdapterInfo adapter, AdapterConfiguration target, string label, IWin32Window? owner)
    {
        var errors = ConfigurationValidator.Validate(target);
        if (errors.Count > 0)
        {
            Dialogs.Error(owner, Loc.T("apply.configInvalid"), string.Join(Environment.NewLine, errors));
            return false;
        }

        // Vor der UAC-Abfrage prüfen, ob die Adresse schon auf einem anderen Adapter liegt.
        if (AddressConflicts.Find(Adapters.GetAll(), adapter.Id, target) is { } conflict)
        {
            Dialogs.Warning(owner, conflict);
            return false;
        }

        ApplyResult? result;
        var pending = Coordinator.ApplyAsync(adapter, target, label); // setzt IsBusy synchron
        BusyChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            result = await pending;
        }
        finally
        {
            BusyChanged?.Invoke(this, EventArgs.Empty);
        }

        owner = Dialogs.Alive(owner);
        if (result is null)
        {
            Notify?.Invoke(Loc.T("apply.cancelled"), ToolTipIcon.Info);
            return false;
        }

        RaiseAdaptersChanged();
        if (result.Success)
        {
            if (Settings.ShowNotifications)
            {
                Notify?.Invoke(result.Message, ToolTipIcon.Info);
            }

            return true;
        }

        Log.Warn($"{label} auf {adapter.Name}: {result.Outcome} – {result.Message}");
        Dialogs.ShowApplyResult(owner, result, Paths.LogDirectory);
        return false;
    }
}
