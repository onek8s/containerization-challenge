using System.Drawing.Printing;

namespace LabelStation;

public sealed class MainForm : Form
{
    private readonly StationSettings _settings;
    private readonly PrintJournal _journal;

    private readonly TextBox _orderNumber = new() { Width = 220, CharacterCasing = CharacterCasing.Upper };
    private readonly ComboBox _carrier = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _shipToName = new() { Width = 220 };
    private readonly TextBox _shipToAddress = new() { Width = 220, Height = 60, Multiline = true };
    private readonly NumericUpDown _parcels = new() { Width = 80, Minimum = 1, Maximum = 99, Value = 1 };
    private readonly NumericUpDown _weight = new() { Width = 80, Minimum = 0.1m, Maximum = 999m, DecimalPlaces = 1, Value = 1m };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, BackColor = Color.LightGray, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly ListBox _history = new() { Dock = DockStyle.Fill };
    private readonly ToolStripStatusLabel _status = new();

    public MainForm(StationSettings settings, PrintJournal journal)
    {
        _settings = settings;
        _journal = journal;

        Text = $"Label Station - {_settings.StationId}";
        Width = 900;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _carrier.Items.AddRange(new object[] { "PostNord", "DHL", "DB Schenker", "UPS", "Matkahuolto" });
        _carrier.SelectedItem = _settings.DefaultCarrier;
        if (_carrier.SelectedIndex < 0)
        {
            _carrier.SelectedIndex = 0;
        }

        BuildLayout();
        RefreshHistory();
        UpdateStatus();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F9)
            {
                PrintLabels();
                e.Handled = true;
            }
        };
    }

    private void BuildLayout()
    {
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&Settings...", null, (_, _) => OpenSettings());
        fileMenu.DropDownItems.Add("Open print &history", null, (_, _) => OpenHistoryFile());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());
        menu.Items.Add(fileMenu);

        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8), AutoSize = true };
        AddRow(form, "Order number", _orderNumber);
        AddRow(form, "Carrier", _carrier);
        AddRow(form, "Recipient", _shipToName);
        AddRow(form, "Address", _shipToAddress);
        AddRow(form, "Parcels", _parcels);
        AddRow(form, "Weight / parcel (kg)", _weight);

        var previewButton = new Button { Text = "Preview", Width = 100 };
        previewButton.Click += (_, _) => ShowPreview();
        var printButton = new Button { Text = "Print (F9)", Width = 100 };
        printButton.Click += (_, _) => PrintLabels();
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(previewButton);
        buttons.Controls.Add(printButton);
        form.Controls.Add(new Label());
        form.Controls.Add(buttons);

        var historyGroup = new GroupBox { Text = "Recent labels", Dock = DockStyle.Fill };
        historyGroup.Controls.Add(_history);

        var left = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
        left.Panel1.Controls.Add(form);
        left.Panel2.Controls.Add(historyGroup);

        var main = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 420 };
        main.Panel1.Controls.Add(left);
        main.Panel2.Controls.Add(_preview);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(main);
        Controls.Add(statusStrip);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private static void AddRow(TableLayoutPanel panel, string caption, Control control)
    {
        panel.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) });
        panel.Controls.Add(control);
    }

    private ShippingLabel? ReadLabel(int parcelIndex)
    {
        if (string.IsNullOrWhiteSpace(_orderNumber.Text) || string.IsNullOrWhiteSpace(_shipToName.Text))
        {
            MessageBox.Show(this, "Order number and recipient are required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return new ShippingLabel(
            _orderNumber.Text.Trim(),
            (string)_carrier.SelectedItem!,
            _shipToName.Text.Trim(),
            _shipToAddress.Text.Trim(),
            parcelIndex,
            (int)_parcels.Value,
            _weight.Value);
    }

    private SizeF LabelSizeMm => new(_settings.LabelWidthMm, _settings.LabelHeightMm);

    private void ShowPreview()
    {
        var label = ReadLabel(1);
        if (label is null)
        {
            return;
        }

        const float pixelsPerMm = 8f;
        var bitmap = new Bitmap((int)(LabelSizeMm.Width * pixelsPerMm), (int)(LabelSizeMm.Height * pixelsPerMm));
        bitmap.SetResolution(pixelsPerMm * 25.4f, pixelsPerMm * 25.4f);
        using (var g = Graphics.FromImage(bitmap))
        {
            LabelRenderer.Render(g, label, LabelSizeMm, _settings.StationId);
        }

        _preview.Image?.Dispose();
        _preview.Image = bitmap;
    }

    private void PrintLabels()
    {
        var first = ReadLabel(1);
        if (first is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_settings.PrinterName))
        {
            MessageBox.Show(this, "No label printer selected. Choose one in File > Settings.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenSettings();
            return;
        }

        var count = (int)_parcels.Value;
        var next = 1;

        using var document = new PrintDocument();
        document.DocumentName = $"Label {first.OrderNumber}";
        document.PrinterSettings.PrinterName = _settings.PrinterName;
        document.DefaultPageSettings.PaperSize = new PaperSize(
            "Label", MmToHundredthsInch(_settings.LabelWidthMm), MmToHundredthsInch(_settings.LabelHeightMm));
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.OriginAtMargins = false;

        document.PrintPage += (_, e) =>
        {
            var label = first with { ParcelIndex = next };
            LabelRenderer.Render(e.Graphics!, label, LabelSizeMm, _settings.StationId);
            _journal.Record(label, _settings.PrinterName, _settings.StationId);
            next++;
            e.HasMorePages = next <= count;
        };

        try
        {
            if (!document.PrinterSettings.IsValid)
            {
                throw new InvalidPrinterException(document.PrinterSettings);
            }

            document.Print();
            _status.Text = $"Printed {count} label(s) for {first.OrderNumber}.";
            _orderNumber.Clear();
            _shipToName.Clear();
            _shipToAddress.Clear();
            _parcels.Value = 1;
            _orderNumber.Focus();
        }
        catch (Exception ex)
        {
            _journal.RecordFailure(first, ex);
            MessageBox.Show(this, $"Printing failed: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        RefreshHistory();
    }

    private static int MmToHundredthsInch(int mm) => (int)Math.Round(mm / 25.4 * 100);

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.Save();
            Text = $"Label Station - {_settings.StationId}";
            UpdateStatus();
        }
    }

    private void OpenHistoryFile()
    {
        if (File.Exists(_journal.HistoryFile))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_journal.HistoryFile) { UseShellExecute = true });
        }
    }

    private void RefreshHistory()
    {
        _history.Items.Clear();
        foreach (var line in _journal.ReadRecent(50))
        {
            _history.Items.Add(line.Replace(";", "  "));
        }
    }

    private void UpdateStatus()
    {
        _status.Text = string.IsNullOrEmpty(_settings.PrinterName)
            ? "No printer selected"
            : $"Printer: {_settings.PrinterName}  ({_settings.LabelWidthMm} x {_settings.LabelHeightMm} mm)";
    }
}
