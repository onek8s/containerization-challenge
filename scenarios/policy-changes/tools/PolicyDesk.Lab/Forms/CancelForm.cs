namespace PolicyDesk.Lab.Forms;

public sealed class CancelForm : ChangeForm
{
    private readonly ComboBox _reason;
    private readonly TextBox _iban, _refund;
    private decimal? _refundAmount;

    public CancelForm(Policy policy, string user) : base(policy, user, "Cancel policy")
    {
        _reason = AddList("Reason:", "Customer request", "Vehicle sold", "Death of policyholder", "Duplicate policy");
        _iban = AddText("Refund IBAN:", 260);
        _refund = AddText("Refund:", 120, readOnly: true);
        AddSaveButtons("Calculate");
        ButtonByText("Calculate").Click += (_, _) => Calculate();
        _reason.SelectedIndexChanged += (_, _) => ClearRefund();
        EffectiveDate.TextChanged += (_, _) => ClearRefund();
    }

    protected override string EndorsementType => "Cancellation";

    private void ClearRefund()
    {
        _refundAmount = null;
        _refund.Text = "";
    }

    private bool ReasonAllowed()
    {
        if ((string?)_reason.SelectedItem == "Vehicle sold" && !Policy.IsMotor)
        {
            Error(this, "Reason Vehicle sold is only valid for motor policies.");
            return false;
        }
        if (_reason.SelectedItem is null)
        {
            Error(this, "Select a reason.");
            return false;
        }
        return true;
    }

    private void Calculate()
    {
        if (!TryEffective(out var effective) || !ReasonAllowed()) return;
        var periodStart = Policy.StartDate.AddYears(Math.Max(0, (int)((effective - Policy.StartDate).TotalDays / 365.25)));
        var remaining = Math.Max(0, (periodStart.AddYears(1) - effective).Days);
        _refundAmount = Math.Round(Policy.AnnualPremium * remaining / 365m, 2);
        _refund.Text = _refundAmount.Value.ToString("N2") + " EUR";
    }

    protected override bool CheckInput(DateTime effective)
    {
        if (!ReasonAllowed()) return false;
        if (_refundAmount is null)
        {
            Error(this, "Calculate the refund before saving.");
            return false;
        }
        if (_refundAmount > 0 && _iban.Text.Trim().Length > 0 && !IsValidIban(_iban.Text))
        {
            Error(this, "Refund IBAN is not valid.");
            return false;
        }
        return true;
    }

    protected override bool Confirm() =>
        MessageBox.Show(this, $"Cancel policy {Policy.Number} from {EffectiveDate.Text.Trim()}? This cannot be undone.",
            Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    protected override void Apply(Policy policy) => policy.Status = "Cancelled";

    protected override string EndorsementMessage(string number) =>
        $"Endorsement {number} created. Refund {_refundAmount:N2} EUR " +
        (_iban.Text.Trim().Length > 0 ? "to " + _iban.Text.Trim() + "." : "by cheque.");
}
