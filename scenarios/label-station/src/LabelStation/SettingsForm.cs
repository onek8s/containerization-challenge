using System.Drawing.Printing;

namespace LabelStation;

public sealed class SettingsForm : Form
{
    private readonly StationSettings _settings;
    private readonly ComboBox _printer = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _width = new() { Minimum = 25, Maximum = 210, Width = 80 };
    private readonly NumericUpDown _height = new() { Minimum = 25, Maximum = 300, Width = 80 };
    private readonly TextBox _carrier = new() { Width = 260 };
    private readonly TextBox _stationId = new() { Width = 260 };

    public SettingsForm(StationSettings settings)
    {
        _settings = settings;

        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            _printer.Items.Add(name);
        }
        _printer.SelectedItem = settings.PrinterName;

        _width.Value = settings.LabelWidthMm;
        _height.Value = settings.LabelHeightMm;
        _carrier.Text = settings.DefaultCarrier;
        _stationId.Text = settings.StationId;

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(10) };
        Add(layout, "Label printer", _printer);
        Add(layout, "Label width (mm)", _width);
        Add(layout, "Label height (mm)", _height);
        Add(layout, "Default carrier", _carrier);
        Add(layout, "Station ID", _stationId);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Apply();
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(new Label());
        layout.Controls.Add(buttons);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private static void Add(TableLayoutPanel panel, string caption, Control control)
    {
        panel.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left });
        panel.Controls.Add(control);
    }

    private void Apply()
    {
        _settings.PrinterName = _printer.SelectedItem as string ?? "";
        _settings.LabelWidthMm = (int)_width.Value;
        _settings.LabelHeightMm = (int)_height.Value;
        _settings.DefaultCarrier = _carrier.Text.Trim();
        _settings.StationId = _stationId.Text.Trim();
    }
}
