using IPSwitch.Core.Localization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using IPSwitch.Core.Elevation;
using IPSwitch.App.Infrastructure;
using IPSwitch.App.UI;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Storage;

namespace IPSwitch.App;

/// <summary>
/// Hauptkontext der Anwendung: Tray-Symbol und Kontextmenü. Das Menü wird erst beim Öffnen aufgebaut;
/// Änderungen an der Netzwerkkonfiguration werden über Systemereignisse erkannt (kein Polling).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int MaxTooltipLength = 127;

    private readonly AppController _controller;
    private readonly SingleInstance _instance;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _debounce;
    private readonly SynchronizationContext _ui;
    private readonly Font _boldFont;
    private readonly bool _elevated = ElevationInfo.IsElevated;
    private MainForm? _mainForm;

    public TrayApplicationContext(AppPaths paths, SingleInstance instance, bool showWindow)
    {
        _instance = instance;
        _controller = new AppController(paths);
        UI.Modern.Theme.Configure(_controller.Settings.Theme);

        _menu = new ContextMenuStrip { ShowItemToolTips = true };
        _menu.Items.Add("IP-Switch");
        _boldFont = new Font(_menu.Font, FontStyle.Bold);
        _menu.Opening += (_, e) =>
        {
            BuildMenu();
            e.Cancel = false;
        };
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIcons.Tray,
            Text = "IP-Switch",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnNotifyIconClick;
        _notifyIcon.BalloonTipClicked += (_, _) => ShowMainForm();

        _controller.Notify = (message, icon) => _notifyIcon.ShowBalloonTip(5000, "IP-Switch", Truncate(message, 250), icon);
        _controller.AdaptersChanged += (_, _) => UpdateTooltip();

        _debounce = new System.Windows.Forms.Timer { Interval = 1500 };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _controller.RaiseAdaptersChanged();
        };

        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        _instance.Listen(() => _ui.Post(_ => ShowMainForm(), null));

        Autostart.RefreshPathIfEnabled();
        if (_controller.Settings.AdminAutostart && SafeAutostartState())
        {
            // Z. B. nach einem MSI-Update wieder eingetragener normaler Autostart: würde mit der Aufgabe konkurrieren.
            try
            {
                Autostart.Set(false);
            }
            catch (Exception ex)
            {
                Log.Warn("Doppelter Autostart konnte nicht bereinigt werden: " + ex.Message);
            }
        }

        UpdateTooltip();

        if (!showWindow && _controller.ConsumeLoadError() is { } loadError)
        {
            _controller.Notify(loadError, ToolTipIcon.Warning);
        }

        // Beim Abmelden bzw. wenn der Restart Manager (z. B. bei Deinstallation/Update) zum Schließen auffordert.
        Microsoft.Win32.SystemEvents.SessionEnded += OnSessionEnded;

        if (showWindow)
        {
            _ui.Post(_ => ShowMainForm(), null);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            Microsoft.Win32.SystemEvents.SessionEnded -= OnSessionEnded;
            _debounce.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _boldFont.Dispose();
            _mainForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnSessionEnded(object? sender, Microsoft.Win32.SessionEndedEventArgs e) =>
        _ui.Post(_ => ExitThread(), null);

    private void OnNetworkChanged(object? sender, EventArgs e) =>
        _ui.Post(_ =>
        {
            // Mehrere Ereignisse kurz hintereinander zu einer Aktualisierung zusammenfassen.
            _debounce.Stop();
            _debounce.Start();
        }, null);

    private void OnNotifyIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Linksklick öffnet ebenfalls das Menü (NotifyIcon bietet dafür keine öffentliche API).
        var show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (show is not null)
        {
            show.Invoke(_notifyIcon, null);
        }
        else
        {
            ShowMainForm();
        }
    }

    private void UpdateTooltip()
    {
        try
        {
            var connected = _controller.GetAdapters().Where(a => a.IsConnected && a.Addresses.Count > 0).Take(3)
                .Select(a => $"{Truncate(a.Name, 22)}: {a.PrimaryAddressText}");
            _notifyIcon.Text = Truncate(string.Join("\n", new[] { Title }.Concat(connected)), MaxTooltipLength);
        }
        catch (Exception ex)
        {
            Log.Warn("Tooltip konnte nicht aktualisiert werden: " + ex.Message);
        }
    }

    private void ShowMainForm()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_controller);
            _mainForm.FormClosed += (_, _) =>
            {
                _mainForm?.Dispose();
                _mainForm = null;
            };
            _mainForm.Show();
        }
        else
        {
            if (_mainForm.WindowState == FormWindowState.Minimized)
            {
                _mainForm.WindowState = FormWindowState.Normal;
            }

            _mainForm.RefreshAdapters();
        }

        _mainForm.TopMost = true;
        _mainForm.Activate();
        _mainForm.TopMost = false;
    }

    private IWin32Window? Owner => _mainForm is { IsDisposed: false, Visible: true } ? _mainForm : null;

    private string Title => _elevated ? Loc.T("tray.titleAdmin") : "IP-Switch";

    // ---------------- Menü ----------------

    private void BuildMenu()
    {
        _menu.SuspendLayout();
        _menu.RightToLeft = Loc.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        foreach (var old in _menu.Items.Cast<ToolStripItem>().ToList())
        {
            old.Dispose();
        }

        _menu.Items.Clear();

        IReadOnlyList<AdapterInfo> adapters;
        try
        {
            adapters = _controller.GetAdapters();
        }
        catch (Exception ex)
        {
            Log.Error("Adapter konnten nicht gelesen werden", ex);
            adapters = [];
        }

        var profiles = _controller.Profiles.Profiles;
        var busy = _controller.IsBusy;

        var header = new ToolStripMenuItem(Title) { Enabled = false, Font = _boldFont };
        _menu.Items.Add(header);
        if (busy)
        {
            _menu.Items.Add(new ToolStripMenuItem(Loc.T("status.busy")) { Enabled = false });
        }

        // Schnellzugriff: Profile mit vorhandenem zugeordnetem Adapter.
        var quick = profiles
            .Select(p => (Profile: p, Adapter: AdapterService.Match(adapters, p.Adapter)))
            .Where(x => x.Adapter is not null)
            .ToList();
        _menu.Items.Add(new ToolStripSeparator());
        foreach (var (profile, adapter) in quick)
        {
            var item = new ToolStripMenuItem($"{profile.Name}  →  {adapter!.Name}")
            {
                ToolTipText = AdapterText.ProfileSummary(profile) + (profile.Description is { } d ? "\n" + d : string.Empty),
                Enabled = !busy,
            };
            item.Click += async (_, _) => await _controller.ApplyProfileAsync(profile, adapter, Owner);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(BuildQuickAccessSetupMenu(profiles, adapters));

        _menu.Items.Add(new ToolStripSeparator());
        if (adapters.Count == 0)
        {
            _menu.Items.Add(new ToolStripMenuItem(Loc.T("tray.noAdapters")) { Enabled = false });
        }

        // Verbundene Adapter und Adapter mit zugeordneten Profilen direkt, alle übrigen gesammelt in einem Untermenü.
        var withProfiles = quick.Select(q => q.Adapter!.Id).ToHashSet();
        var primary = adapters.Where(a => a.IsConnected || withProfiles.Contains(a.Id)).ToList();
        foreach (var adapter in primary)
        {
            _menu.Items.Add(BuildAdapterMenu(adapter, profiles, adapters, busy));
        }

        var secondary = adapters.Except(primary).ToList();
        if (secondary.Count > 0)
        {
            var more = new ToolStripMenuItem(Loc.T("tray.moreAdapters", secondary.Count)) { Image = AppIcons.Disconnected };
            foreach (var adapter in secondary)
            {
                more.DropDownItems.Add(BuildAdapterMenu(adapter, profiles, adapters, busy));
            }

            _menu.Items.Add(more);
        }

        _menu.Items.Add(new ToolStripSeparator());
        var open = new ToolStripMenuItem(Loc.T("tray.open")) { Font = _boldFont };
        open.Click += (_, _) => ShowMainForm();
        var newProfile = new ToolStripMenuItem(Loc.T("tray.newProfile"));
        newProfile.Click += async (_, _) => await _controller.EditProfileAsync(new IpProfile(), isNew: true, Owner);
        _menu.Items.AddRange([open, newProfile]);

        _menu.Items.Add(new ToolStripSeparator());
        var settings = new ToolStripMenuItem(Loc.T("tray.settings"));
        var showAll = new ToolStripMenuItem(Loc.T("tray.showAll")) { Checked = _controller.Settings.ShowAllAdapters };
        showAll.Click += (_, _) =>
        {
            _controller.Settings.ShowAllAdapters = !_controller.Settings.ShowAllAdapters;
            _controller.SaveSettings();
            _controller.RaiseAdaptersChanged();
        };
        var notifications = new ToolStripMenuItem(Loc.T("tray.notifications")) { Checked = _controller.Settings.ShowNotifications };
        notifications.Click += (_, _) =>
        {
            _controller.Settings.ShowNotifications = !_controller.Settings.ShowNotifications;
            _controller.SaveSettings();
        };
        var adminAutostartOn = _controller.Settings.AdminAutostart;
        var autostart = new ToolStripMenuItem(Loc.T("tray.autostart")) { Checked = adminAutostartOn || SafeAutostartState() };
        autostart.Click += async (_, _) =>
        {
            try
            {
                if (_controller.Settings.AdminAutostart)
                {
                    // Abschalten schließt den Start mit Administratorrechten ein.
                    await SetAdminAutostartAsync(enable: false, restoreNormalAutostart: false);
                }
                else
                {
                    Autostart.Set(!Autostart.IsEnabled);
                }
            }
            catch (Exception ex)
            {
                Dialogs.Error(Owner, Loc.T("tray.autostartFailed", ex.Message));
            }
        };

        var adminCapable = ElevationInfo.IsAdministratorAccount;
        var adminAutostart = new ToolStripMenuItem(Loc.T("tray.adminAutostart"))
        {
            Checked = adminAutostartOn,
            Enabled = adminCapable,
            ToolTipText = adminCapable
                ? Loc.T("tray.adminAutostartTip")
                : Loc.T("tray.adminOnly"),
        };
        adminAutostart.Click += async (_, _) => await SetAdminAutostartAsync(!_controller.Settings.AdminAutostart, restoreNormalAutostart: true);

        var dataFolder = new ToolStripMenuItem(Loc.T("action.openDataFolder"));
        dataFolder.Click += (_, _) => Dialogs.OpenFolder(_controller.Paths.DataDirectory);
        settings.DropDownItems.AddRange([showAll, notifications, new ToolStripSeparator(), autostart, adminAutostart]);
        if (!_elevated && adminCapable)
        {
            var restart = new ToolStripMenuItem(Loc.T("tray.restartAdmin")) { Enabled = !busy };
            restart.Click += (_, _) => RestartElevated();
            settings.DropDownItems.Add(restart);
        }

        var appearance = new ToolStripMenuItem(Loc.T("tray.appearance"));
        foreach (var (mode, label) in new[] { (ThemeMode.System, Loc.T("theme.system")), (ThemeMode.Light, Loc.T("theme.light")), (ThemeMode.Dark, Loc.T("theme.dark")) })
        {
            var item = new ToolStripMenuItem(label) { Checked = _controller.Settings.Theme == mode };
            item.Click += (_, _) => SetTheme(mode);
            appearance.DropDownItems.Add(item);
        }

        // Sprachen in ihrer eigenen Schreibweise, damit jeder seine Sprache wiederfindet.
        var language = new ToolStripMenuItem(Loc.T("tray.language"));
        var systemName = Loc.Languages.First(l => l.Code == Loc.SystemLanguage).NativeName;
        var systemLanguage = new ToolStripMenuItem(Loc.T("language.system", systemName)) { Checked = _controller.Settings.Language is null };
        systemLanguage.Click += (_, _) => SetLanguage(null);
        language.DropDownItems.Add(systemLanguage);
        language.DropDownItems.Add(new ToolStripSeparator());
        foreach (var lang in Loc.Languages)
        {
            var item = new ToolStripMenuItem(lang.NativeName) { Checked = _controller.Settings.Language == lang.Code };
            item.Click += (_, _) => SetLanguage(lang.Code);
            language.DropDownItems.Add(item);
        }

        settings.DropDownItems.AddRange([new ToolStripSeparator(), appearance, language, dataFolder]);

        var help = new ToolStripMenuItem(Loc.T("tray.help"));
        help.Click += (_, _) => HelpForm.ShowTopic(null);
        var about = new ToolStripMenuItem(Loc.T("action.about"));
        about.Click += (_, _) => Dialogs.Info(Owner, AppInfo.AboutText(_controller.Paths.DataDirectory));
        var exit = new ToolStripMenuItem(Loc.T("tray.exit"));
        exit.Click += (_, _) => ExitThread();
        _menu.Items.AddRange([settings, help, about, new ToolStripSeparator(), exit]);
        _menu.ResumeLayout();
    }

    private ToolStripMenuItem BuildAdapterMenu(AdapterInfo adapter, IReadOnlyList<IpProfile> profiles, IReadOnlyList<AdapterInfo> adapters, bool busy)
    {
        var menu = new ToolStripMenuItem(AdapterText.MenuLabel(adapter))
        {
            Image = adapter.IsConnected ? AppIcons.Connected : AppIcons.Disconnected,
            ToolTipText = adapter.Description,
        };
        var items = menu.DropDownItems;

        ToolStripMenuItem Info(string text) => new(text) { Enabled = false };
        items.Add(Info(string.Join(" · ", new[] { adapter.StatusText, adapter.TypeText, adapter.SpeedText }.Where(s => s.Length > 0))));
        items.Add(Info(Loc.T("tray.mode", adapter.ModeText)));
        foreach (var address in adapter.Addresses)
        {
            items.Add(Info($"IPv4: {address.Address}/{address.PrefixLength}"));
        }

        if (adapter.Gateways.Count > 0)
        {
            items.Add(Info(Loc.T("tray.gateway", string.Join(", ", adapter.Gateways))));
        }

        if (adapter.DnsServers.Count > 0)
        {
            items.Add(Info("DNS: " + string.Join(", ", adapter.DnsServers)));
        }

        items.Add(new ToolStripSeparator());

        var assigned = profiles.Where(p => AdapterService.Match(adapters, p.Adapter)?.Id == adapter.Id).ToList();
        foreach (var profile in assigned)
        {
            items.Add(ProfileItem(Loc.T("tray.applyProfile", profile.Name), profile, adapter, busy));
        }

        var others = profiles.Except(assigned).ToList();
        var otherMenu = new ToolStripMenuItem(Loc.T(assigned.Count > 0 ? "tray.applyOther" : "tray.applyAny")) { Enabled = !busy && others.Count > 0 };
        foreach (var profile in others)
        {
            otherMenu.DropDownItems.Add(ProfileItem(profile.Name, profile, adapter, busy, confirmForeign: true));
        }

        items.Add(otherMenu);

        var dhcp = new ToolStripMenuItem(Loc.T("tray.enableDhcp")) { Enabled = !busy };
        dhcp.Click += async (_, _) => await _controller.EnableDhcpAsync(adapter, Owner);
        items.Add(dhcp);

        items.Add(new ToolStripSeparator());
        var capture = new ToolStripMenuItem(Loc.T("tray.capture"));
        capture.Click += async (_, _) => await _controller.CaptureAdapterAsync(adapter, Owner);
        items.Add(capture);

        var latest = SafeLatestBackup(adapter);
        var restore = new ToolStripMenuItem(Loc.T("tray.restoreLatest"))
        {
            Enabled = !busy && latest is not null,
            ToolTipText = latest is null
                ? Loc.T("tray.noBackup")
                : $"{latest.CreatedAt.LocalDateTime:g} – {latest.Reason}\n{latest.Configuration.Summary}",
        };
        restore.Click += async (_, _) =>
        {
            if (latest is not null && Dialogs.Confirm(Owner,
                    Loc.T("confirm.restore", latest.CreatedAt.LocalDateTime.ToString("g"), adapter.Name, latest.Configuration.Summary)))
            {
                await _controller.RestoreBackupAsync(latest, adapter, Owner);
            }
        };
        items.Add(restore);

        return menu;
    }

    /// <summary>Wechselt das Farbschema; ein geöffnetes Verwaltungsfenster wird neu aufgebaut.</summary>
    private void SetTheme(ThemeMode mode)
    {
        _controller.Settings.Theme = mode;
        _controller.SaveSettings();
        UI.Modern.Theme.Configure(mode);
        RebuildWindows();
    }

    /// <summary>Wechselt die Oberflächensprache (null = wie Windows); geöffnete Fenster werden neu aufgebaut.</summary>
    private void SetLanguage(string? code)
    {
        _controller.Settings.Language = code;
        _controller.SaveSettings();
        Loc.SetLanguage(code);
        UpdateTooltip();
        RebuildWindows();
    }

    private void RebuildWindows()
    {
        HelpForm.CloseIfOpen();
        if (_mainForm is { IsDisposed: false })
        {
            var bounds = _mainForm.Bounds;
            var state = _mainForm.WindowState;
            _mainForm.Close();
            ShowMainForm();
            _mainForm!.WindowState = state;
            if (state == FormWindowState.Normal)
            {
                _mainForm.Bounds = bounds;
            }
        }
    }

    /// <summary>
    /// "Schnellzugriff einrichten": pro Profil den Adapter wählen, mit dem es oben im Menü angeboten wird.
    /// Die Auswahl speichert nur die Zuordnung – angewendet wird erst über den Schnellzugriff.
    /// </summary>
    private ToolStripMenuItem BuildQuickAccessSetupMenu(IReadOnlyList<IpProfile> profiles, IReadOnlyList<AdapterInfo> adapters)
    {
        var setup = new ToolStripMenuItem(Loc.T("tray.quickSetup"))
        {
            Enabled = profiles.Count > 0,
            ToolTipText = profiles.Count > 0
                ? Loc.T("tray.quickSetupTip")
                : Loc.T("tray.noProfiles"),
        };

        foreach (var profile in profiles)
        {
            var bound = AdapterService.Match(adapters, profile.Adapter);
            var profileItem = new ToolStripMenuItem(profile.Name + (bound is null ? string.Empty : $"  →  {bound.Name}"))
            {
                ToolTipText = AdapterText.ProfileSummary(profile),
            };

            var none = new ToolStripMenuItem(Loc.T("assign.none")) { Checked = profile.Adapter is null };
            none.Click += (_, _) => AssignProfile(profile, null);
            profileItem.DropDownItems.Add(none);
            profileItem.DropDownItems.Add(new ToolStripSeparator());

            foreach (var adapter in adapters)
            {
                var adapterItem = new ToolStripMenuItem(adapter.Name + (adapter.IsConnected ? string.Empty : "  " + Loc.T("assign.disconnected")))
                {
                    Checked = bound?.Id == adapter.Id,
                    ToolTipText = $"{adapter.Description}\n{adapter.PrimaryAddressText} ({adapter.ModeText})",
                };
                adapterItem.Click += (_, _) => AssignProfile(profile, adapter);
                profileItem.DropDownItems.Add(adapterItem);
            }

            if (profile.Adapter is not null && bound is null)
            {
                // Zugeordneter Adapter ist derzeit nicht vorhanden (oder als Hilfsadapter ausgeblendet).
                profileItem.DropDownItems.Add(new ToolStripMenuItem(
                    Loc.T("adapter.missing", profile.Adapter.LastKnownName ?? profile.Adapter.InterfaceId)) { Checked = true, Enabled = false });
            }

            setup.DropDownItems.Add(profileItem);
        }

        return setup;
    }

    private void AssignProfile(IpProfile profile, AdapterInfo? adapter)
    {
        if (_controller.AssignProfile(profile.Id, adapter) is { } error)
        {
            Dialogs.Error(Owner, Loc.T("assign.failed", profile.Name), error);
            return;
        }

        if (_controller.Settings.ShowNotifications)
        {
            _controller.Notify?.Invoke(adapter is null
                ? Loc.T("assign.removed", profile.Name)
                : Loc.T("assign.done", profile.Name, adapter.Name), ToolTipIcon.Info);
        }
    }

    private ToolStripMenuItem ProfileItem(string text, IpProfile profile, AdapterInfo adapter, bool busy, bool confirmForeign = false)
    {
        var item = new ToolStripMenuItem(text)
        {
            Enabled = !busy,
            ToolTipText = AdapterText.ProfileSummary(profile) + (profile.Description is { } d ? "\n" + d : string.Empty),
        };
        item.Click += async (_, _) =>
        {
            if (confirmForeign && profile.Adapter is not null &&
                !Dialogs.Confirm(Owner, Loc.T("confirm.foreignAdapterShort", profile.Name, adapter.Name)))
            {
                return;
            }

            await _controller.ApplyProfileAsync(profile, adapter, Owner);
        };
        return item;
    }

    /// <summary>
    /// Richtet den Start mit Administratorrechten ein bzw. entfernt ihn. Der normale Autostart (Run-Schlüssel)
    /// wird dabei ab- bzw. auf Wunsch wieder eingeschaltet, damit nie zwei Autostarts gleichzeitig aktiv sind.
    /// </summary>
    private async Task SetAdminAutostartAsync(bool enable, bool restoreNormalAutostart)
    {
        if (enable && !Dialogs.Confirm(Owner,
                Loc.T("adminAutostart.confirm") + "\n\n" +
                (_elevated ? string.Empty : Loc.T("adminAutostart.confirmUac") + "\n\n") +
                Loc.T("common.continue")))
        {
            return;
        }

        var result = await _controller.Coordinator.SetAdminAutostartAsync(enable);
        if (result is null)
        {
            _controller.Notify?.Invoke(Loc.T("settings.cancelled"), ToolTipIcon.Info);
            return;
        }

        if (!result.Success)
        {
            Dialogs.Error(Owner, result.Message);
            return;
        }

        _controller.Settings.AdminAutostart = enable;
        _controller.SaveSettings();
        try
        {
            Autostart.Set(!enable && restoreNormalAutostart);
        }
        catch (Exception ex)
        {
            Log.Warn("Normaler Autostart konnte nicht angepasst werden: " + ex.Message);
        }

        if (!enable)
        {
            _controller.Notify?.Invoke(Loc.T(_elevated ? "adminAutostart.disabledRunning" : "adminAutostart.disabled"), ToolTipIcon.Info);
        }
        else if (_elevated)
        {
            _controller.Notify?.Invoke(result.Message, ToolTipIcon.Info);
        }
        else if (Dialogs.Confirm(Owner, Loc.T("adminAutostart.restartNow")))
        {
            RestartElevated();
        }
    }

    /// <summary>
    /// Startet IP-Switch mit Administratorrechten neu – bevorzugt über die geplante Aufgabe (ohne UAC-Abfrage),
    /// sonst per UAC. Die neue Instanz wartet, bis diese Instanz beendet ist.
    /// </summary>
    private void RestartElevated()
    {
        var started = _controller.Settings.AdminAutostart && new AdminAutostartTask().TryStart(ElevationInfo.CurrentUserSid);
        if (!started)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath, Program.AutostartSwitch)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _controller.Notify?.Invoke(Loc.T("restart.cancelled"), ToolTipIcon.Info);
                return;
            }
            catch (Exception ex)
            {
                Dialogs.Error(Owner, Loc.T("restart.failed", ex.Message));
                return;
            }
        }

        Log.Info("Neustart mit Administratorrechten.");
        ExitThread();
    }

    private ConfigurationBackup? SafeLatestBackup(AdapterInfo adapter)
    {
        try
        {
            return _controller.Backups.Latest(adapter.Id);
        }
        catch (Exception ex)
        {
            Log.Warn("Sicherungen nicht lesbar: " + ex.Message);
            return null;
        }
    }

    private static bool SafeAutostartState()
    {
        try
        {
            return Autostart.IsEnabled;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
