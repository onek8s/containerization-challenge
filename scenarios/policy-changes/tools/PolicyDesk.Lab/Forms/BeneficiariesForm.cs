namespace PolicyDesk.Lab.Forms;

public sealed class BeneficiariesForm : ChangeForm
{
    private readonly ListView _grid;
    private readonly TextBox _name, _birth, _share;

    public BeneficiariesForm(Policy policy, string user) : base(policy, user, "Beneficiaries")
    {
        _grid = AddGrid("Beneficiaries:", 110, ("Name", 170), ("Date of birth", 90), ("Share %", 60));
        _name = AddText("Name:", 220);
        _birth = AddText("Date of birth:", 100);
        _share = AddText("Share %:", 50);
        AddSaveButtons("Add", "Remove all");
        ButtonByText("Add").Click += (_, _) => AddRow();
        ButtonByText("Remove all").Click += (_, _) => RemoveAll();
        foreach (var b in policy.Beneficiaries)
            _grid.Items.Add(new ListViewItem(new[] { b.Name, b.BirthDate.ToString(DateFormat), b.Share.ToString() }));
    }

    protected override string EndorsementType => "Beneficiaries";

    private void AddRow()
    {
        if (_name.Text.Trim().Length < 2)
        {
            Error(this, "Name is required.");
            return;
        }
        if (!TryDate(_birth.Text, out var birth) || birth > DateTime.Today)
        {
            Error(this, "Date of birth is not a valid date.");
            return;
        }
        if (!int.TryParse(_share.Text.Trim(), out var share) || share is < 1 or > 100)
        {
            Error(this, "Share must be between 1 and 100.");
            return;
        }
        _grid.Items.Add(new ListViewItem(new[] { _name.Text.Trim(), birth.ToString(DateFormat), share.ToString() }));
        _name.Clear();
        _birth.Clear();
        _share.Clear();
    }

    private void RemoveAll()
    {
        if (_grid.Items.Count == 0) return;
        if (MessageBox.Show(this, "Remove all beneficiaries?", Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            _grid.Items.Clear();
    }

    protected override bool CheckInput(DateTime effective)
    {
        var total = _grid.Items.Cast<ListViewItem>().Sum(i => int.Parse(i.SubItems[2].Text));
        if (_grid.Items.Count == 0 || total != 100)
        {
            Error(this, $"Beneficiary shares must total 100 % (now {total} %).");
            return false;
        }
        return true;
    }

    protected override void Apply(Policy policy) =>
        policy.Beneficiaries = _grid.Items.Cast<ListViewItem>().Select(i => new Beneficiary
        {
            Name = i.SubItems[0].Text,
            BirthDate = DateTime.ParseExact(i.SubItems[1].Text, DateFormat, null),
            Share = int.Parse(i.SubItems[2].Text),
        }).ToList();
}
