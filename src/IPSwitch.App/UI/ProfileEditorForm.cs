using IPSwitch.Core.Localization;
using IPSwitch.App.Infrastructure;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Profiles;
using IPSwitch.Core.Validation;

namespace IPSwitch.App.UI;

/// <summary>Dialog zum Anlegen und Bearbeiten eines Profils mit Live-Validierung.</summary>
internal sealed class ProfileEditorForm : Form
{
    private readonly AppController _controller;
    private readonly IpProfile _original;
    private readonly TextBox _name = new();
    private readonly TextBox _description = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
    private readonly ComboBox _adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly RadioButton _dhcp = new() { Text = Loc.T("editor.dhcp"), AutoSize = true };
    private readonly RadioButton _static = new() { Text = Loc.T("editor.static"), AutoSize = true };
    private readonly TextBox _ip = new();
    private readonly TextBox _mask = new();
    private readonly TextBox _gateway = new();
    private readonly TextBox _dns1 = new();
    private readonly TextBox _dns2 = new();
    private readonly Label _maskHint = Hint(string.Empty);
    private readonly Label _dnsHint = Hint(string.Empty);
    private readonly ErrorProvider _errors;
    private readonly HashSet<Control> _touched = [];
    private readonly Dictionary<ProfileField, Control> _fieldControls;
    private bool _showAllErrors;

    public ProfileEditorForm(AppController controller, IpProfile profile, bool isNew, IReadOnlyList<AdapterInfo> adapters)
    {
        _controller = controller;
        _original = profile.Clone();
        _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, Icon = SystemIcons.Error };
        _fieldControls = new()
        {
            [ProfileField.Name] = _name,
            [ProfileField.Description] = _description,
            [ProfileField.Adapter] = _adapter,
            [ProfileField.IpAddress] = _ip,
            [ProfileField.SubnetMask] = _mask,
            [ProfileField.Gateway] = _gateway,
            [ProfileField.PreferredDns] = _dns1,
            [ProfileField.AlternateDns] = _dns2,
        };

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = isNew ? Loc.T("editor.titleNew") : Loc.T("editor.titleEdit", profile.Name);
        Icon = AppIcons.Window;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        BuildLayout(adapters);
        LoadProfile(profile, adapters);
        Modern.Theme.Apply(this);
        ResumeLayout(false);
        PerformLayout();

        foreach (var control in _fieldControls.Values)
        {
            control.Leave += (_, _) =>
            {
                _touched.Add(control);
                ValidateLive();
            };
        }

        foreach (var box in new[] { _name, _description, _ip, _mask, _gateway, _dns1, _dns2 })
        {
            box.TextChanged += (_, _) => ValidateLive();
        }

        _mask.TextChanged += (_, _) => UpdateMaskHint();
        HelpRequested += (_, e) =>
        {
            HelpForm.ShowTopic("profiles");
            e.Handled = true;
        };
        _dhcp.CheckedChanged += (_, _) => UpdateMode();
        _adapter.SelectedIndexChanged += (_, _) => ValidateLive();
        UpdateMode();
        UpdateMaskHint();
    }

    /// <summary>Das gespeicherte Profil (nach Klick auf Speichern).</summary>
    public IpProfile? SavedProfile { get; private set; }

    /// <summary>true, wenn "Speichern und anwenden" gewählt wurde.</summary>
    public bool ApplyRequested { get; private set; }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(20, 0, 0, 0),
    };

    private void BuildLayout(IReadOnlyList<AdapterInfo> adapters)
    {
        const int fieldWidth = 300;
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void Row(string label, Control control, Control? hint = null)
        {
            control.Width = fieldWidth;
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(3, 4, 3, 4);
            var row = grid.RowCount++;
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 4, 12, 4) }, 0, row);
            grid.Controls.Add(control, 1, row);
            if (hint is not null)
            {
                grid.Controls.Add(hint, 2, row);
            }
        }

        void Span(Control control, int topMargin = 4)
        {
            var row = grid.RowCount++;
            control.Margin = new Padding(3, topMargin, 3, 2);
            grid.Controls.Add(control, 0, row);
            grid.SetColumnSpan(control, 3);
        }

        _name.MaxLength = ProfileValidator.MaxNameLength;
        _description.MaxLength = ProfileValidator.MaxDescriptionLength;
        _description.Height = _description.Font.Height * 3 + 8;
        foreach (var box in new[] { _ip, _mask, _gateway, _dns1, _dns2 })
        {
            box.MaxLength = 18;
        }

        Row(Loc.T("editor.name"), _name);
        Row(Loc.T("editor.description"), _description, Hint(Loc.T("common.optional")));
        Row(Loc.T("editor.adapter"), _adapter, Hint(Loc.T("common.optional")));
        _adapter.DropDownWidth = 480;

        Span(new Label { Text = Loc.T("editor.sectionIpv4"), AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 14);
        Span(_dhcp);
        Span(_static, 0);
        Row(Loc.T("editor.ip"), _ip, Hint(Loc.T("editor.ipHint")));
        Row(Loc.T("editor.mask"), _mask, _maskHint);
        Row(Loc.T("editor.gateway"), _gateway, Hint(Loc.T("common.optional")));

        Span(new Label { Text = Loc.T("editor.sectionDns"), AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 14);
        Row(Loc.T("editor.dns1"), _dns1);
        Row(Loc.T("editor.dns2"), _dns2);
        Span(_dnsHint, 0);
        _dnsHint.Margin = new Padding(3, 0, 3, 8);

        var save = new Modern.ModernButton { Text = Loc.T("editor.save"), Glyph = Modern.Glyphs.Save, Kind = Modern.ButtonKind.Primary, Margin = new Padding(8, 0, 0, 0) };
        var saveApply = new Modern.ModernButton { Text = Loc.T("editor.saveApply"), Glyph = Modern.Glyphs.Accept, Kind = Modern.ButtonKind.Success, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new Modern.ModernButton { Text = Loc.T("common.cancel"), DialogResult = DialogResult.Cancel, Margin = new Padding(8, 0, 0, 0) };
        save.Click += (_, _) => Save(apply: false);
        saveApply.Click += (_, _) => Save(apply: true);
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange([cancel, save, saveApply]);
        Span(buttons, 12);

        var tips = new ToolTip();
        tips.SetToolTip(_mask, Loc.T("editor.tipMask"));
        tips.SetToolTip(_adapter, Loc.T("editor.tipAdapter"));
        tips.SetToolTip(saveApply, Loc.T("editor.tipSaveApply"));

        Controls.Add(grid);
    }

    private void LoadProfile(IpProfile profile, IReadOnlyList<AdapterInfo> adapters)
    {
        _name.Text = profile.Name;
        _description.Text = profile.Description ?? string.Empty;
        _ip.Text = profile.IpAddress ?? string.Empty;
        _mask.Text = profile.SubnetMask ?? string.Empty;
        _gateway.Text = profile.Gateway ?? string.Empty;
        _dns1.Text = profile.PreferredDns ?? string.Empty;
        _dns2.Text = profile.AlternateDns ?? string.Empty;
        _dhcp.Checked = profile.UseDhcp;
        _static.Checked = !profile.UseDhcp;

        _adapter.Items.Add(new AdapterChoice(null, Loc.T("editor.noAdapter"), Present: true));
        var selected = 0;
        var match = AdapterService.Match(adapters, profile.Adapter);
        foreach (var adapter in adapters)
        {
            var index = _adapter.Items.Add(new AdapterChoice(AdapterBinding.FromAdapter(adapter),
                $"{adapter.Name} – {adapter.Description} ({adapter.StatusText})", Present: true));
            if (match is not null && match.Id == adapter.Id)
            {
                selected = index;
            }
        }

        if (profile.Adapter is not null && match is null)
        {
            selected = _adapter.Items.Add(new AdapterChoice(profile.Adapter,
                Loc.T("adapter.missing", profile.Adapter.LastKnownName ?? profile.Adapter.InterfaceId), Present: false));
        }

        _adapter.SelectedIndex = selected;
    }

    private void UpdateMode()
    {
        var isStatic = _static.Checked;
        _ip.Enabled = _mask.Enabled = _gateway.Enabled = isStatic;
        _dnsHint.Text = isStatic
            ? Loc.T("editor.dnsHintStatic")
            : Loc.T("editor.dnsHintDhcp");
        ValidateLive();
    }

    private void UpdateMaskHint() =>
        _maskHint.Text = IpValidator.TryParseSubnet(_mask.Text, out var prefix, out _)
            ? $"≙ /{prefix} ({IpValidator.PrefixToMask(prefix)})"
            : Loc.T("editor.maskHint");

    private IpProfile Collect()
    {
        var profile = _original.Clone();
        profile.Name = _name.Text;
        profile.Description = _description.Text;
        profile.UseDhcp = _dhcp.Checked;
        profile.IpAddress = _ip.Text;
        profile.SubnetMask = _mask.Text;
        profile.Gateway = _gateway.Text;
        profile.PreferredDns = _dns1.Text;
        profile.AlternateDns = _dns2.Text;
        profile.Adapter = (_adapter.SelectedItem as AdapterChoice)?.Binding?.Clone();
        return profile;
    }

    private ValidationResult ValidateLive()
    {
        var result = ProfileValidator.Validate(ProfileConverter.Normalize(Collect()), _controller.Profiles.Profiles);
        foreach (var (field, control) in _fieldControls)
        {
            var show = _showAllErrors || _touched.Contains(control);
            _errors.SetError(control, show ? result.ErrorFor(field) ?? string.Empty : string.Empty);
        }

        return result;
    }

    private void Save(bool apply)
    {
        _showAllErrors = true;
        var result = ValidateLive();
        if (!result.IsValid)
        {
            FocusFirstError(result);
            Dialogs.Error(this, Loc.T("editor.invalid") + Environment.NewLine + Environment.NewLine + result.Summary);
            return;
        }

        if (apply && _adapter.SelectedItem is not AdapterChoice { Binding: not null, Present: true })
        {
            var message = _adapter.SelectedItem is AdapterChoice { Binding: not null }
                ? Loc.T("editor.adapterMissing")
                : Loc.T("editor.adapterRequired");
            _errors.SetError(_adapter, message);
            _adapter.Focus();
            Dialogs.Warning(this, message);
            return;
        }

        var profile = Collect();
        ValidationResult saveResult;
        try
        {
            saveResult = _controller.Profiles.Save(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogs.Error(this, Loc.T("editor.saveFailed", ex.Message));
            return;
        }

        if (!saveResult.IsValid)
        {
            FocusFirstError(saveResult);
            Dialogs.Error(this, Loc.T("editor.invalid") + Environment.NewLine + Environment.NewLine + saveResult.Summary);
            return;
        }

        SavedProfile = _controller.Profiles.Get(profile.Id);
        ApplyRequested = apply;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void FocusFirstError(ValidationResult result)
    {
        if (result.Errors.Count > 0 && _fieldControls.TryGetValue(result.Errors[0].Field, out var control) && control.Enabled)
        {
            control.Focus();
            if (control is TextBox box)
            {
                box.SelectAll();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _errors.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record AdapterChoice(AdapterBinding? Binding, string Text, bool Present)
    {
        public override string ToString() => Text;
    }
}
