using IPSwitch.Core.Localization;
using IPSwitch.App.Infrastructure;
using IPSwitch.App.UI.Modern;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Storage;
using IPSwitch.Core.Validation;

namespace IPSwitch.App.UI;

/// <summary>
/// Verwaltungsfenster: links Kopfbereich und Profil-Kacheln (mit ⋮-Menü), rechts "Profil zuweisen auf"
/// mit Zieladapter, aktueller Konfiguration und den Schaltflächen zum Anwenden, unten die Adapter als Kacheln.
/// Wird nur bei Bedarf erzeugt und beim Schließen freigegeben.
/// </summary>
internal sealed class MainForm : Form
{
    private static string FileFilter => Loc.T("file.filter");

    private readonly AppController _controller;
    private readonly Palette _p = Theme.Current;
    private readonly ProfileGrid _profiles = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly AdapterTiles _tiles = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly SearchBox _search = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 36, Margin = new Padding(16, 0, 8, 0) };
    private readonly Label _profileCount;
    private readonly AdapterPicker _picker = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Dictionary<string, Label> _configValues = [];
    private readonly Label _residual;
    private readonly ModernButton _apply;
    private readonly ModernButton _dhcp;
    private readonly ModernButton _edit;
    private readonly ModernButton _duplicate;
    private readonly ModernButton _delete;
    private readonly ModernButton _capture;
    private readonly ModernButton _backups;
    private readonly ModernButton _details;
    private readonly Label _status;
    private readonly Label _rights;
    private readonly ContextMenuStrip _settingsMenu;
    private readonly ToolTip _tips = new();
    private ContextMenuStrip? _profileMenu;
    private IReadOnlyList<AdapterInfo> _adapterCache = [];
    private bool _syncing;

    public MainForm(AppController controller)
    {
        _controller = controller;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.75f);
        Text = "IP-Switch";
        Icon = AppIcons.Window;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1300, 840);
        MinimumSize = new Size(900, 640); // wird in FitToScreen() auf 1290 × 790 bzw. die Bildschirmgröße gesetzt
        KeyPreview = true;
        BackColor = _p.Background;
        ForeColor = _p.Text;
        Padding = new Padding(24, 20, 24, 16);
        Theme.ApplyTitleBar(this);
        Theme.ApplyDirection(this);

        _settingsMenu = CreateMenu(
            (Loc.T("action.import"), Glyphs.Import, Import),
            (Loc.T("action.exportAll"), Glyphs.Export, () => Export(_controller.Profiles.Profiles)),
            (null, null, null),
            (Loc.T("action.openDataFolder"), Glyphs.Folder, () => Dialogs.OpenFolder(_controller.Paths.DataDirectory)),
            (null, null, null),
            (Loc.T("action.helpF1"), Glyphs.Help, () => HelpForm.ShowTopic(null)),
            (Loc.T("action.about"), Glyphs.Info, () => Dialogs.Info(this, AppInfo.AboutText(_controller.Paths.DataDirectory))));

        // ---------- Kopfkarte (links oben) ----------
        _edit = IconButton(Glyphs.Edit, Loc.T("main.tipEdit"), async (_, _) => await EditSelectedAsync());
        _duplicate = IconButton(Glyphs.Copy, Loc.T("main.tipDuplicate"), (_, _) => DuplicateSelected());
        _delete = IconButton(Glyphs.Delete, Loc.T("main.tipDelete"), (_, _) => DeleteSelected());
        _delete.Kind = ButtonKind.Danger;
        var settings = IconButton(Glyphs.Settings, Loc.T("main.tipMore"), (_, _) => { });
        settings.Click += (_, _) => _settingsMenu.Show(settings, new Point(0, settings.Height + 4));
        var newProfile = new ModernButton { Text = Loc.T("main.newProfile"), Glyph = Glyphs.Add, Kind = ButtonKind.Primary, MinimumHeight = 38 };
        newProfile.Click += async (_, _) => await NewProfileAsync();
        _tips.SetToolTip(newProfile, Loc.T("main.tipNew"));

        var separator = new Panel { Width = 1, Height = 28, BackColor = _p.Border, Margin = new Padding(12, 5, 8, 0) };
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        var helpButton = IconButton(Glyphs.Help, Loc.T("action.helpF1"), (_, _) => HelpForm.ShowTopic(null));
        actions.Controls.AddRange([newProfile, separator, _edit, _duplicate, _delete, settings, helpButton]);
        HelpRequested += (_, e) =>
        {
            HelpForm.ShowTopic(null);
            e.Handled = true;
        };

        var title = new Label { Text = Loc.T("main.title"), AutoSize = true, Font = new Font("Segoe UI Semibold", 20f), Margin = new Padding(0, 0, 0, 2) };
        var subtitle = new Label { Text = Loc.T("main.subtitle"), AutoSize = true, ForeColor = _p.TextSecondary, Margin = Padding.Empty };
        var titles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        titles.Controls.AddRange([title, subtitle]);

        var headerGrid = Grid(2, 2);
        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerGrid.Controls.Add(titles, 0, 0);
        headerGrid.Controls.Add(actions, 1, 0);
        // Oben angedockt (nicht "Fill"), damit die Karte mitwächst, wenn die Schaltflächen in die zweite Zeile rutschen.
        headerGrid.Dock = DockStyle.Top;
        headerGrid.AutoSize = true;
        headerGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var header = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 18, 20, 18), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 16) };
        header.Controls.Add(headerGrid);

        // Bei schmalem Fenster die Schaltflächen unter den Titel setzen, statt den Titel zu quetschen.
        void ArrangeHeader()
        {
            var available = header.ClientSize.Width - header.Padding.Horizontal;
            var stacked = available < titles.PreferredSize.Width + actions.PreferredSize.Width + LogicalToDeviceUnits(24);
            var target = stacked ? new TableLayoutPanelCellPosition(0, 1) : new TableLayoutPanelCellPosition(1, 0);
            if (headerGrid.GetCellPosition(actions) != target)
            {
                headerGrid.SuspendLayout();
                headerGrid.SetCellPosition(actions, target);
                actions.Anchor = stacked ? AnchorStyles.Left : AnchorStyles.Right;
                actions.Margin = stacked ? new Padding(-4, 12, 0, 0) : Padding.Empty;
                headerGrid.ResumeLayout(true);

                // Wir befinden uns mitten im Layoutdurchgang des Fensters, in dem eine erneute Berechnung
                // unterdrückt wird. Daher direkt danach nachholen, sonst behält die Karte ihre alte Höhe.
                if (IsHandleCreated)
                {
                    BeginInvoke(() =>
                    {
                        header.PerformLayout();
                        header.Parent?.PerformLayout();
                    });
                }
            }
        }

        header.SizeChanged += (_, _) => ArrangeHeader();

        // ---------- Profilzeile: "Profile: N", Suche, + Neu, ⋯ ----------
        var profilesTitle = new Label { Text = Loc.T("main.profiles"), AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        _profileCount = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), Anchor = AnchorStyles.Left, Margin = new Padding(4, 0, 0, 0) };
        var addSmall = new ModernButton { Text = Loc.T("main.new"), Glyph = Glyphs.Add, MinimumHeight = 36, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        addSmall.Click += async (_, _) => await NewProfileAsync();
        var more = new ModernButton { Text = "⋯", Kind = ButtonKind.Ghost, MinimumHeight = 36, Anchor = AnchorStyles.Left, Margin = new Padding(4, 0, 0, 0) };
        more.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        more.AccessibleName = Loc.T("main.moreActions");
        _tips.SetToolTip(more, Loc.T("main.tipMore"));
        more.Click += (_, _) => _settingsMenu.Show(more, new Point(0, more.Height + 4));

        var listHeader = Grid(5, 1);
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        listHeader.Controls.Add(profilesTitle, 0, 0);
        listHeader.Controls.Add(_profileCount, 1, 0);
        listHeader.Controls.Add(_search, 2, 0);
        listHeader.Controls.Add(addSmall, 3, 0);
        listHeader.Controls.Add(more, 4, 0);
        listHeader.Dock = DockStyle.Fill;
        listHeader.AutoSize = true;
        listHeader.Margin = new Padding(0, 0, 0, 12);

        var left = Grid(1, 3);
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(header, 0, 0);
        left.Controls.Add(listHeader, 0, 1);
        left.Controls.Add(_profiles, 0, 2);
        left.Dock = DockStyle.Fill;
        left.Margin = new Padding(0, 0, 18, 0);

        // ---------- Profil zuweisen auf (rechts) ----------
        var applyTitle = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 18) };
        applyTitle.Controls.Add(new GlyphLabel(Glyphs.Forward, 20f, _p.Text) { Margin = new Padding(0, 0, 12, 0) });
        applyTitle.Controls.Add(new Label { Text = Loc.T("main.applyTitle"), AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), Margin = new Padding(0, 4, 0, 0) });

        var refresh = new ModernButton { Text = Loc.T("main.refresh"), Glyph = Glyphs.Refresh, Margin = Padding.Empty };
        refresh.Click += (_, _) => RefreshAdapters();
        _tips.SetToolTip(refresh, Loc.T("main.tipRefresh"));
        var refreshRow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 16), WrapContents = false };
        refreshRow.Controls.Add(refresh);

        _residual = new Label { AutoSize = true, ForeColor = _p.Warning, Visible = false, Margin = new Padding(0, 8, 0, 0), MaximumSize = new Size(360, 0) };
        _details = SmallButton(Glyphs.Info, Loc.T("main.details"), (_, _) => ShowDetails());
        _capture = SmallButton(Glyphs.Save, Loc.T("main.capture"), async (_, _) => await CaptureAsync());
        _backups = SmallButton(Glyphs.History, Loc.T("main.backups"), async (_, _) => await ShowBackupsAsync());
        var configCard = BuildConfigCard();

        _dhcp = new ModernButton { Text = Loc.T("action.enableDhcp"), Glyph = Glyphs.Sync, Kind = ButtonKind.Dhcp, AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0), MinimumHeight = 48 };
        _dhcp.Font = new Font("Segoe UI Semibold", 10f);
        _dhcp.Click += async (_, _) => await EnableDhcpAsync();
        _tips.SetToolTip(_dhcp, Loc.T("main.tipDhcp"));
        _apply = new ModernButton { Text = Loc.T("main.apply"), Glyph = Glyphs.Accept, Kind = ButtonKind.Apply, AutoSize = false, Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0), MinimumHeight = 48 };
        _apply.Font = new Font("Segoe UI Semibold", 10f);
        _apply.Click += async (_, _) => await ApplySelectedAsync();

        var buttonRow = Grid(2, 1);
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        buttonRow.Controls.Add(_dhcp, 0, 0);
        buttonRow.Controls.Add(_apply, 1, 0);
        buttonRow.Dock = DockStyle.Fill;
        buttonRow.Height = 48;
        buttonRow.Margin = new Padding(0, 16, 0, 10);

        _status = new Label { Dock = DockStyle.Fill, AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 0, 2) };
        _rights = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = _p.TextSecondary,
            Font = new Font("Segoe UI", 8.75f),
            Text = ElevationInfo.IsElevated
                ? Loc.T("main.rightsAdmin")
                : Loc.T("main.rightsUac"),
        };

        // Oberer Teil (Ziel und aktuelle Konfiguration) scrollt bei Platzmangel; die Schaltflächen samt
        // Statuszeile sind unten fest verankert und bleiben damit immer sichtbar und bedienbar.
        var right = Grid(1, 5);
        for (var i = 0; i < 5; i++)
        {
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        right.Controls.Add(applyTitle, 0, 0);
        right.Controls.Add(new Label { Text = Loc.T("main.targetAdapter"), AutoSize = true, Font = new Font("Segoe UI Semibold", 9.75f), Margin = new Padding(0, 0, 0, 6) }, 0, 1);
        right.Controls.Add(_picker, 0, 2);
        right.Controls.Add(refreshRow, 0, 3);
        right.Controls.Add(configCard, 0, 4);
        right.Dock = DockStyle.Top;
        right.AutoSize = true;
        right.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var rightScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        rightScroll.Controls.Add(right);
        Theme.ApplyNativeTheme(rightScroll);

        var applyArea = Grid(1, 3);
        applyArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        applyArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        applyArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        applyArea.Controls.Add(buttonRow, 0, 0);
        applyArea.Controls.Add(_status, 0, 1);
        applyArea.Controls.Add(_rights, 0, 2);
        applyArea.Dock = DockStyle.Bottom;
        applyArea.AutoSize = true;
        applyArea.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        applyArea.Padding = new Padding(0, 12, 0, 0);

        var rightCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 20, 22, 16), Margin = Padding.Empty };
        rightCard.Controls.Add(rightScroll);
        rightCard.Controls.Add(applyArea);

        var body = Grid(2, 1);
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        body.Controls.Add(left, 0, 0);
        body.Controls.Add(rightCard, 1, 0);
        body.Dock = DockStyle.Fill;

        // ---------- Adapter (unten, volle Breite) ----------
        var adapterTitle = new Label { Text = Loc.T("main.adapters"), AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8) };
        _tiles.Height = 82;
        _tiles.BackColor = _p.Card;
        var adapterCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 14) };
        adapterCard.Controls.Add(_tiles);
        adapterCard.Controls.Add(adapterTitle);
        var adapterSection = new Panel { Dock = DockStyle.Bottom, Height = 162, Padding = new Padding(0, 16, 0, 0) };
        adapterSection.Controls.Add(adapterCard);

        Controls.Add(body);
        Controls.Add(adapterSection);

        _profiles.AdapterName = profile => profile.Adapter is null ? null : AdapterText.ProfileAdapter(profile, _adapterCache);
        ResumeLayout(false);
        PerformLayout();

        // ---------- Ereignisse ----------
        _profiles.SelectionChanged += (_, _) => OnProfileSelectionChanged();
        _profiles.ItemActivated += async (_, _) => await EditSelectedAsync();
        _profiles.MenuRequested += (_, e) => ShowProfileMenu(e.Profile, e.ScreenLocation);
        _picker.SelectedIndexChanged += (_, _) => OnAdapterSelectionChanged();
        _tiles.AdapterSelected += (_, adapter) => _picker.SelectedItem = _adapterCache.FirstOrDefault(a => a.Id == adapter.Id);
        _search.Box.TextChanged += (_, _) => RefreshProfiles();
        _controller.Profiles.Changed += OnProfilesChanged;
        _controller.AdaptersChanged += OnAdaptersChanged;
        _controller.BusyChanged += OnBusyChanged;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToScreen();
        RefreshAdapters();
        if (_controller.ConsumeLoadError() is { } loadError)
        {
            Dialogs.Warning(this, loadError);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.F5:
                RefreshAdapters();
                e.Handled = true;
                break;
            case Keys.N when e.Control:
                _ = NewProfileAsync();
                e.Handled = true;
                break;
            case Keys.F when e.Control:
                _search.Box.Focus();
                e.Handled = true;
                break;
            case Keys.Delete when _profiles.Focused:
                DeleteSelected();
                e.Handled = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.Profiles.Changed -= OnProfilesChanged;
            _controller.AdaptersChanged -= OnAdaptersChanged;
            _controller.BusyChanged -= OnBusyChanged;
            _tips.Dispose();
            _settingsMenu.Dispose();
            _profileMenu?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Mindestgröße 1290 × 790 (alles passt nebeneinander), aber nie größer als der verfügbare Bildschirm –
    /// sonst ließe sich das Fenster z. B. auf Laptops mit 150 % Skalierung nicht mehr vollständig anzeigen.
    /// </summary>
    private void FitToScreen()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(
            Math.Min(LogicalToDeviceUnits(1290), area.Width),
            Math.Min(LogicalToDeviceUnits(790), area.Height));

        if (Width > area.Width || Height > area.Height)
        {
            Bounds = new Rectangle(area.Left, area.Top, Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        }
        else if (StartPosition == FormStartPosition.CenterScreen)
        {
            Location = new Point(area.Left + ((area.Width - Width) / 2), area.Top + ((area.Height - Height) / 2));
        }
    }

    public void RefreshAdapters()
    {
        var selectedId = _picker.SelectedAdapter?.Id;
        _adapterCache = _controller.GetAdapters();

        _syncing = true;
        _picker.BeginUpdate();
        _picker.Items.Clear();
        foreach (var adapter in _adapterCache)
        {
            _picker.Items.Add(adapter);
        }

        var reselect = _adapterCache.FirstOrDefault(a => a.Id == selectedId) ?? _adapterCache.FirstOrDefault(a => a.IsConnected) ?? _adapterCache.FirstOrDefault();
        _picker.SelectedItem = reselect;
        _picker.EndUpdate();
        _syncing = false;

        _tiles.SetAdapters(_adapterCache, reselect?.Id);
        RefreshProfiles();
        OnAdapterSelectionChanged();
    }

    // ---------- Aufbau ----------

    private CardPanel BuildConfigCard()
    {
        var card = new CardPanel { Dock = DockStyle.Fill, BackColor = _p.Inner, Padding = new Padding(16, 14, 16, 10), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
        var grid = Grid(2, 0);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.Dock = DockStyle.Fill;
        grid.AutoSize = true;

        var heading = new Label { Text = Loc.T("main.currentConfig"), AutoSize = true, Font = new Font("Segoe UI Semibold", 10f), Margin = new Padding(0, 0, 0, 8) };
        grid.Controls.Add(heading, 0, grid.RowCount++);
        grid.SetColumnSpan(heading, 2);

        foreach (var key in new[] { "cfg.status", "cfg.mode", "cfg.ip", "cfg.mask", "cfg.gateway", "cfg.dns1", "cfg.dns2" })
        {
            var row = grid.RowCount++;
            grid.Controls.Add(new Label { Text = Loc.T(key), AutoSize = true, ForeColor = _p.TextSecondary, Margin = new Padding(0, 3, 16, 3) }, 0, row);
            var value = new Label { AutoSize = true, Margin = new Padding(0, 3, 0, 3), UseMnemonic = false };
            _configValues[key] = value;
            grid.Controls.Add(value, 1, row);
        }

        var residualRow = grid.RowCount++;
        grid.Controls.Add(_residual, 0, residualRow);
        grid.SetColumnSpan(_residual, 2);

        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(-6, 10, 0, 0), Dock = DockStyle.Fill };
        links.Controls.AddRange([_details, _capture, _backups]);
        var linkRow = grid.RowCount++;
        grid.Controls.Add(links, 0, linkRow);
        grid.SetColumnSpan(links, 2);

        card.Controls.Add(grid);
        return card;
    }

    private ModernButton IconButton(string glyph, string tooltip, EventHandler onClick)
    {
        var button = new ModernButton { Glyph = glyph, Kind = ButtonKind.Ghost, MinimumHeight = 38, Margin = new Padding(4, 0, 0, 0) };
        button.Click += onClick;
        button.AccessibleName = tooltip;
        _tips.SetToolTip(button, tooltip);
        return button;
    }

    private ModernButton SmallButton(string glyph, string text, EventHandler onClick)
    {
        var button = new ModernButton { Glyph = glyph, Text = text, Kind = ButtonKind.Ghost, MinimumHeight = 30, Margin = new Padding(0, 0, 2, 0) };
        button.Font = new Font("Segoe UI", 9f);
        button.Click += onClick;
        return button;
    }

    private ContextMenuStrip CreateMenu(params (string? Text, string? Glyph, Action? Action)[] entries)
    {
        var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(), ShowImageMargin = true, Font = Font };
        foreach (var (text, glyph, action) in entries)
        {
            if (text is null)
            {
                menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            var item = new ToolStripMenuItem(text) { Tag = glyph };
            item.Click += (_, _) => action?.Invoke();
            menu.Items.Add(item);
        }

        return menu;
    }

    private static TableLayoutPanel Grid(int columns, int rows)
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = columns,
            RowCount = rows,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false,
        };

        if (columns == 1)
        {
            // Einspaltig: volle verfügbare Breite statt Breite des breitesten Inhalts.
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }

        return grid;
    }

    // ---------- ⋮-Menü eines Profils ----------

    private void ShowProfileMenu(IpProfile profile, Point screen)
    {
        _profileMenu?.Dispose();
        var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(), ShowImageMargin = true, Font = Font };

        if (_picker.SelectedAdapter is { } target)
        {
            var apply = new ToolStripMenuItem(Loc.T("menu.applyTo", target.Name)) { Tag = Glyphs.Accept, Enabled = !_controller.IsBusy };
            apply.Click += async (_, _) => await ApplySelectedAsync();
            menu.Items.Add(apply);
        }

        var bound = AdapterService.Match(_adapterCache, profile.Adapter);
        var assign = new ToolStripMenuItem(Loc.T("menu.assign")) { Tag = Glyphs.Link };
        var none = new ToolStripMenuItem(Loc.T("assign.none")) { Checked = profile.Adapter is null };
        none.Click += (_, _) => Assign(profile, null);
        assign.DropDownItems.Add(none);
        assign.DropDownItems.Add(new ToolStripSeparator());
        foreach (var adapter in _adapterCache)
        {
            var item = new ToolStripMenuItem(adapter.Name + (adapter.IsConnected ? string.Empty : "  " + Loc.T("assign.disconnected")))
            {
                Checked = bound?.Id == adapter.Id,
                ToolTipText = $"{adapter.Description}\n{adapter.PrimaryAddressText} ({adapter.ModeText})",
            };
            item.Click += (_, _) => Assign(profile, adapter);
            assign.DropDownItems.Add(item);
        }

        if (profile.Adapter is not null && bound is null)
        {
            assign.DropDownItems.Add(new ToolStripMenuItem(Loc.T("adapter.missing", profile.Adapter.LastKnownName ?? profile.Adapter.InterfaceId)) { Checked = true, Enabled = false });
        }

        assign.DropDown.Renderer = new ModernMenuRenderer();
        menu.Items.Add(assign);
        menu.Items.Add(new ToolStripSeparator());

        foreach (var (text, glyph, action) in new (string, string, Action)[]
                 {
                     (Loc.T("menu.edit"), Glyphs.Edit, () => _ = EditSelectedAsync()),
                     (Loc.T("menu.duplicate"), Glyphs.Copy, DuplicateSelected),
                     (Loc.T("menu.export"), Glyphs.Export, () => Export([profile])),
                 })
        {
            var item = new ToolStripMenuItem(text) { Tag = glyph };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var delete = new ToolStripMenuItem(Loc.T("menu.delete")) { Tag = Glyphs.Delete };
        delete.Click += (_, _) => DeleteSelected();
        menu.Items.Add(delete);

        _profileMenu = menu;
        menu.Show(screen);
    }

    private void Assign(IpProfile profile, AdapterInfo? adapter)
    {
        if (_controller.AssignProfile(profile.Id, adapter) is { } error)
        {
            Dialogs.Error(this, Loc.T("assign.failed", profile.Name), error);
            return;
        }

        if (adapter is not null)
        {
            _picker.SelectedItem = _adapterCache.FirstOrDefault(a => a.Id == adapter.Id);
        }
    }

    // ---------- Aktualisierung ----------

    private void RefreshProfiles()
    {
        var selectedId = _profiles.SelectedProfile?.Id;
        var all = _controller.Profiles.Profiles;
        var filter = _search.Box.Text.Trim();
        var visible = filter.Length == 0 ? all : all.Where(p => Matches(p, filter)).ToList();

        _profiles.EmptyText = all.Count == 0
            ? Loc.T("main.emptyProfiles")
            : Loc.T("main.noMatch");
        _syncing = true;
        _profiles.SetItems(visible, selectedId);
        _syncing = false;

        _profileCount.Text = filter.Length == 0 ? $"{all.Count}" : Loc.T("main.countFiltered", visible.Count, all.Count);
        OnProfileSelectionChanged();
    }

    private static bool Matches(IpProfile profile, string filter) =>
        new[] { profile.Name, profile.Description, profile.IpAddress, profile.Gateway, profile.PreferredDns, profile.AlternateDns, profile.Adapter?.LastKnownName }
            .Any(v => v?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true);

    private void OnProfilesChanged(object? sender, EventArgs e) => RefreshProfiles();

    private void OnAdaptersChanged(object? sender, EventArgs e) => RefreshAdapters();

    private void OnBusyChanged(object? sender, EventArgs e) => UpdateState();

    private void OnProfileSelectionChanged()
    {
        // Zugeordneten Adapter automatisch als Ziel wählen.
        if (!_syncing && _profiles.SelectedProfile?.Adapter is { } binding &&
            AdapterService.Match(_adapterCache, binding) is { } bound && _picker.SelectedAdapter?.Id != bound.Id)
        {
            _picker.SelectedItem = _adapterCache.First(a => a.Id == bound.Id);
        }

        UpdateState();
    }

    private void OnAdapterSelectionChanged()
    {
        _tiles.SelectedId = _picker.SelectedAdapter?.Id;
        ShowAdapterConfiguration(_picker.SelectedAdapter);
        UpdateState();
    }

    private void ShowAdapterConfiguration(AdapterInfo? adapter)
    {
        void Set(string key, string? value) => _configValues[key].Text = string.IsNullOrWhiteSpace(value) ? "–" : value;

        if (adapter is null)
        {
            foreach (var key in _configValues.Keys)
            {
                Set(key, null);
            }

            _residual.Visible = false;
            return;
        }

        var persistent = adapter.PersistentConfiguration;
        var active = adapter.Addresses.FirstOrDefault(a => !a.Address.StartsWith("169.254.", StringComparison.Ordinal)) ?? adapter.Addresses.FirstOrDefault();
        var stored = persistent is { DhcpEnabled: false, Addresses.Count: > 0 } ? persistent.Addresses[0] : null;
        var dns = adapter.DnsServers.Count > 0 ? adapter.DnsServers : persistent?.DnsServers ?? [];

        Set("cfg.status", string.Join(" · ", new[] { adapter.StatusText, adapter.TypeText, adapter.SpeedText }.Where(s => s.Length > 0)));
        Set("cfg.mode", adapter.ModeText);
        Set("cfg.ip", active?.Address ?? stored?.Address);
        Set("cfg.mask", active is not null ? $"{active.SubnetMask}  (/{active.PrefixLength})" :
            stored is not null ? $"{stored.SubnetMask}  (/{IpValidator.MaskToPrefix(stored.SubnetMask)})" : null);
        Set("cfg.gateway", adapter.Gateways.FirstOrDefault() ?? persistent?.Gateways.FirstOrDefault());
        Set("cfg.dns1", dns.ElementAtOrDefault(0) ?? (persistent?.DnsFromDhcp == true ? Loc.T("common.viaDhcp") : null));
        Set("cfg.dns2", dns.ElementAtOrDefault(1));

        var residual = persistent?.ResidualAddresses ?? [];
        _residual.Visible = residual.Count > 0;
        _residual.Text = residual.Count == 0
            ? string.Empty
            : "⚠ " + Loc.T("main.residual", string.Join(", ", residual.Select(r => r.Address)));
    }

    private void UpdateState()
    {
        var busy = _controller.IsBusy;
        var profile = _profiles.SelectedProfile;
        var adapter = _picker.SelectedAdapter;

        _edit.Enabled = _duplicate.Enabled = _delete.Enabled = profile is not null;
        _apply.Enabled = !busy && profile is not null && adapter is not null;
        _dhcp.Enabled = !busy && adapter is not null;
        _capture.Enabled = _backups.Enabled = _details.Enabled = adapter is not null;
        _apply.Text = Loc.T(busy ? "main.applying" : "main.apply");

        _status.Text = busy
            ? Loc.T("status.busy")
            : profile is null
                ? Loc.T("main.statusSelectProfile")
                : adapter is null
                    ? Loc.T("main.statusSelectAdapter")
                    : Loc.T("main.statusReady", profile.Name, adapter.Name);
    }

    // ---------- Adapteraktionen ----------

    private async Task EnableDhcpAsync()
    {
        if (_picker.SelectedAdapter is not { } adapter)
        {
            return;
        }

        if (Dialogs.Confirm(this, Loc.T("confirm.dhcp", adapter.Name)))
        {
            await _controller.EnableDhcpAsync(adapter, this);
        }
    }

    private async Task CaptureAsync()
    {
        if (_picker.SelectedAdapter is { } adapter && await _controller.CaptureAdapterAsync(adapter, this) is { } saved)
        {
            SelectProfile(saved.Id);
        }
    }

    private void ShowDetails()
    {
        if (_picker.SelectedAdapter is not { } adapter)
        {
            return;
        }

        using var dialog = new DetailsDialog(Loc.T("details.title", adapter.Name), AdapterText.Details(adapter), MessageBoxIcon.Information, expanded: true);
        Dialogs.ShowModal(dialog, this);
    }

    private async Task ShowBackupsAsync()
    {
        if (_picker.SelectedAdapter is not { } adapter)
        {
            return;
        }

        ConfigurationBackup? backup;
        using (var picker = new BackupPickerForm(_controller, adapter))
        {
            if (Dialogs.ShowModal(picker, this) != DialogResult.OK || picker.SelectedBackup is null)
            {
                return;
            }

            backup = picker.SelectedBackup;
        }

        if (Dialogs.Confirm(this, Loc.T("confirm.restore", backup.CreatedAt.LocalDateTime.ToString("g"), adapter.Name, backup.Configuration.Summary)))
        {
            await _controller.RestoreBackupAsync(backup, adapter, this);
        }
    }

    // ---------- Profilaktionen ----------

    private async Task NewProfileAsync()
    {
        var profile = new IpProfile { Adapter = _picker.SelectedAdapter is { } a ? AdapterBinding.FromAdapter(a) : null };
        if (await _controller.EditProfileAsync(profile, isNew: true, this) is { } saved)
        {
            SelectProfile(saved.Id);
        }
    }

    private async Task EditSelectedAsync()
    {
        if (_profiles.SelectedProfile is { } profile)
        {
            await _controller.EditProfileAsync(profile, isNew: false, this);
        }
    }

    private void DuplicateSelected()
    {
        if (_profiles.SelectedProfile is not { } profile)
        {
            return;
        }

        try
        {
            SelectProfile(_controller.Profiles.Duplicate(profile.Id).Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Dialogs.Error(this, Loc.T("error.duplicate", ex.Message));
        }
    }

    private void DeleteSelected()
    {
        if (_profiles.SelectedProfile is not { } profile || !Dialogs.Confirm(this, Loc.T("confirm.delete", profile.Name), MessageBoxIcon.Warning))
        {
            return;
        }

        try
        {
            _controller.Profiles.Delete(profile.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(this, Loc.T("error.delete", ex.Message));
        }
    }

    private void Import()
    {
        using var dialog = new OpenFileDialog { Filter = FileFilter, Title = Loc.T("action.importTitle") };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var report = ProfileTransfer.Import(dialog.FileName, _controller.Profiles);
            if (report.Skipped.Count > 0)
            {
                Dialogs.Warning(this, report.Summary);
            }
            else
            {
                Dialogs.Info(this, report.Summary);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Dialogs.Error(this, Loc.T("error.import", ex.Message));
        }
    }

    private void Export(IReadOnlyList<IpProfile> profiles)
    {
        if (profiles.Count == 0)
        {
            Dialogs.Info(this, Loc.T("export.none"));
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = FileFilter,
            Title = profiles.Count == 1 ? Loc.T("export.titleOne", profiles[0].Name) : Loc.T("export.titleAll"),
            FileName = profiles.Count == 1 ? $"{string.Concat(profiles[0].Name.Split(Path.GetInvalidFileNameChars()))}.json" : $"ip-switch-profile-{DateTime.Now:yyyy-MM-dd}.json",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ProfileTransfer.Export(dialog.FileName, profiles);
            Dialogs.Info(this, Loc.T("export.done", profiles.Count, dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(this, Loc.T("error.export", ex.Message));
        }
    }

    private async Task ApplySelectedAsync()
    {
        if (_profiles.SelectedProfile is not { } profile || _picker.SelectedAdapter is not { } adapter)
        {
            return;
        }

        var bound = AdapterService.Match(_adapterCache, profile.Adapter);
        if (bound is not null && bound.Id != adapter.Id &&
            !Dialogs.Confirm(this, Loc.T("confirm.foreignAdapter", profile.Name, bound.Name, adapter.Name)))
        {
            return;
        }

        await _controller.ApplyProfileAsync(profile, adapter, this);
    }

    private void SelectProfile(Guid id)
    {
        if (_search.Box.TextLength > 0 && _profiles.Items.All(p => p.Id != id))
        {
            _search.Box.Clear();
        }

        _profiles.Select(id);
        _profiles.Focus();
    }
}
