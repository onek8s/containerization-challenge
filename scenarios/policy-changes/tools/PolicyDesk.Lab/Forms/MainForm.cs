namespace PolicyDesk.Lab.Forms;

public sealed class MainForm : LabForm
{
    private readonly string _user;
    private readonly TextBox _number;
    private readonly Label _status;

    public MainForm(string user, string environment)
    {
        _user = user;
        Text = $"PolicyDesk 6.4 - {user} [{environment}]";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        _number = AddText("Policy number:", 160);
        var open = AddButtons("Open");
        AcceptButton = open;
        open.Click += (_, _) => OpenPolicy();
        _status = new Label { Left = 14, Top = ClientSize.Height, Width = 480, Text = "Ready" };
        Controls.Add(_status);
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + 26);
    }

    private void OpenPolicy()
    {
        var number = _number.Text.Trim().ToUpperInvariant();
        var policy = LabData.Load().Find(number);
        if (policy is null)
        {
            Error(this, $"Policy {number} was not found.");
            return;
        }
        if (policy.LockedBy is not null && policy.LockedBy != _user)
        {
            Error(this, $"Policy {number} is locked by {policy.LockedBy}.");
            return;
        }
        _status.Text = $"{number} opened";
        using var form = new PolicyForm(number, _user);
        form.ShowDialog(this);
        _status.Text = "Ready";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // The main window stays open on Escape.
        if (e.KeyCode != Keys.Escape) base.OnKeyDown(e);
    }
}
