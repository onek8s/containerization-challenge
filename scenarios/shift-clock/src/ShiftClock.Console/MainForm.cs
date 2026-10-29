using ShiftClock.Shared;

namespace ShiftClock.Console;

public sealed class MainForm : Form
{
    private readonly ServiceClient _client = new();
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly TextBox _filter = new() { Width = 160, PlaceholderText = "Employee no. or name" };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };
    private readonly ToolStripStatusLabel _readerStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _userStatus = new();
    private readonly Button _add = new() { Text = "Add punch...", AutoSize = true };
    private readonly Button _delete = new() { Text = "Delete", AutoSize = true };
    private readonly Button _export = new() { Text = "Export payroll", AutoSize = true };
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 5000 };
    private List<Punch> _punches = new();

    public MainForm()
    {
        Text = "Shift Clock - Supervisor Console";
        Width = 1000;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;

        var refresh = new Button { Text = "Refresh", AutoSize = true };
        refresh.Click += async (_, _) => await LoadPunchesAsync();
        _add.Click += async (_, _) => await AddCorrectionAsync();
        _delete.Click += async (_, _) => await DeleteSelectedAsync();
        _export.Click += async (_, _) => await ExportAsync();
        _filter.TextChanged += (_, _) => BindGrid();
        _from.Value = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        _to.Value = DateTime.Today;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        toolbar.Controls.AddRange([
            new Label { Text = "From", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _from,
            new Label { Text = "To", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, _to,
            refresh, _filter, _add, _delete, _export
        ]);

        var status = new StatusStrip();
        status.Items.AddRange([_readerStatus, _userStatus]);

        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(status);

        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
        Load += async (_, _) =>
        {
            await RefreshStatusAsync();
            await LoadPunchesAsync();
            _statusTimer.Start();
        };
        FormClosed += async (_, _) => await _client.DisposeAsync();
    }

    private SupervisorRequest Period(SupervisorCommand command) => new()
    {
        Command = command,
        From = DateOnly.FromDateTime(_from.Value),
        To = DateOnly.FromDateTime(_to.Value)
    };

    private async Task RefreshStatusAsync()
    {
        try
        {
            var response = await _client.SendAsync(new SupervisorRequest { Command = SupervisorCommand.GetStatus });
            if (response.Status is { } s)
            {
                var reader = s.PortOpen ? $"Reader on {s.PortName} online" : $"Reader on {s.PortName} OFFLINE";
                var last = s.LastBadgeAt is { } at ? $"last badge {s.LastBadgeName} at {at:t}" : "no badges yet";
                _readerStatus.Text = $"{s.Terminal}: {reader}, {last}, {s.PunchesToday} punches today";
                _readerStatus.ForeColor = s.PortOpen ? SystemColors.ControlText : Color.Firebrick;
            }
            UpdateUser();
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            _readerStatus.Text = ex is UnauthorizedAccessException
                ? "Access denied: your account is not allowed to use the Shift Clock console."
                : "Shift Clock service is not running on this computer.";
            _readerStatus.ForeColor = Color.Firebrick;
        }
    }

    private void UpdateUser()
    {
        _userStatus.Text = _client.CallerName is null
            ? string.Empty
            : $"{_client.CallerName} ({(_client.IsSupervisor ? "supervisor" : "read only")})";
        _add.Enabled = _delete.Enabled = _export.Enabled = _client.IsSupervisor;
    }

    private async Task LoadPunchesAsync()
    {
        var response = await TrySendAsync(Period(SupervisorCommand.ListPunches));
        if (response is null)
            return;
        _punches = response.Punches ?? new List<Punch>();
        BindGrid();
    }

    private void BindGrid()
    {
        var filter = _filter.Text.Trim();
        _grid.DataSource = _punches
            .Where(p => filter.Length == 0
                || p.EmployeeNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || p.EmployeeName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Timestamp)
            .Select(p => new PunchRow(p))
            .ToList();
        if (_grid.Columns[nameof(PunchRow.Punch)] is { } hidden)
            hidden.Visible = false;
    }

    private async Task AddCorrectionAsync()
    {
        using var dialog = new CorrectionForm((_grid.CurrentRow?.DataBoundItem as PunchRow)?.Punch);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var response = await TrySendAsync(new SupervisorRequest
        {
            Command = SupervisorCommand.AddCorrection,
            Correction = dialog.ToPunch()
        });
        if (response is not null)
            await LoadPunchesAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not PunchRow row)
            return;
        var p = row.Punch;
        if (MessageBox.Show(this, $"Delete {p.Direction} punch of {p.EmployeeName} at {p.Timestamp:g}?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var date = DateOnly.FromDateTime(p.Timestamp);
        var response = await TrySendAsync(new SupervisorRequest
        {
            Command = SupervisorCommand.DeletePunch,
            PunchId = p.Id,
            From = date,
            To = date
        });
        if (response is not null)
            await LoadPunchesAsync();
    }

    private async Task ExportAsync()
    {
        var response = await TrySendAsync(Period(SupervisorCommand.ExportPayroll));
        if (response is not null)
            MessageBox.Show(this, $"Payroll file written to {response.ExportPath}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task<SupervisorResponse?> TrySendAsync(SupervisorRequest request)
    {
        UseWaitCursor = true;
        try
        {
            var response = await _client.SendAsync(request);
            UpdateUser();
            if (!response.Ok)
            {
                MessageBox.Show(this, response.Error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return response;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not reach the Shift Clock service: {ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private sealed class PunchRow(Punch punch)
    {
        public Punch Punch { get; } = punch;
        public string Time => Punch.Timestamp.ToString("ddd yyyy-MM-dd HH:mm");
        public string EmployeeNo => Punch.EmployeeNumber;
        public string Name => Punch.EmployeeName;
        public string Direction => Punch.Direction.ToString();
        public string Source => Punch.Source == PunchSource.Reader ? Punch.Terminal ?? "Reader" : "Correction";
        public string CorrectedBy => Punch.CorrectedBy ?? string.Empty;
        public string Reason => Punch.Reason ?? string.Empty;
    }
}
