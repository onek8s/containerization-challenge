using ShiftClock.Shared;

namespace ShiftClock.Console;

/// <summary>Dialog for adding a missed punch on behalf of an employee.</summary>
public sealed class CorrectionForm : Form
{
    private readonly TextBox _employeeNumber = new() { Width = 200 };
    private readonly TextBox _employeeName = new() { Width = 200 };
    private readonly DateTimePicker _timestamp = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd HH:mm",
        Width = 200
    };
    private readonly ComboBox _direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _reason = new() { Width = 300, Height = 60, Multiline = true };

    public CorrectionForm(Punch? template)
    {
        Text = "Add punch";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _direction.Items.AddRange([PunchDirection.In, PunchDirection.Out]);
        _direction.SelectedIndex = 0;
        if (template is not null)
        {
            _employeeNumber.Text = template.EmployeeNumber;
            _employeeName.Text = template.EmployeeName;
            _timestamp.Value = template.Timestamp;
            _direction.SelectedItem = template.Direction == PunchDirection.In ? PunchDirection.Out : PunchDirection.In;
        }

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_employeeNumber.Text) || string.IsNullOrWhiteSpace(_reason.Text))
            {
                MessageBox.Show(this, "Employee number and reason are required.", Text);
                DialogResult = DialogResult.None;
            }
        };

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        AddRow(layout, "Employee no.", _employeeNumber);
        AddRow(layout, "Name", _employeeName);
        AddRow(layout, "Time", _timestamp);
        AddRow(layout, "Direction", _direction);
        AddRow(layout, "Reason", _reason);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([cancel, ok]);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
    }

    public Punch ToPunch() => new()
    {
        BadgeId = string.Empty,
        EmployeeNumber = _employeeNumber.Text.Trim(),
        EmployeeName = _employeeName.Text.Trim(),
        Timestamp = _timestamp.Value,
        Direction = (PunchDirection)_direction.SelectedItem!,
        Source = PunchSource.Correction,
        Reason = _reason.Text.Trim()
    };

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 8, 0) });
        layout.Controls.Add(control);
    }
}
