namespace PolicyDesk.Lab.Forms;

public sealed class PaymentForm : ChangeForm
{
    private readonly ComboBox _method;
    private readonly TextBox _iban, _holder, _day;

    public PaymentForm(Policy policy, string user) : base(policy, user, "Change payment")
    {
        _method = AddList("Payment method:", "Direct debit", "Invoice");
        _iban = AddText("IBAN:", 260);
        _holder = AddText("Account holder:", 260);
        _day = AddText("Billing day:", 50);
        _method.SelectedIndexChanged += (_, _) => _iban.Enabled = _holder.Enabled = IsDirectDebit;
        _method.SelectedItem = policy.Payment.Method;
        _iban.Text = policy.Payment.Iban;
        _holder.Text = policy.Payment.AccountHolder;
        _day.Text = policy.Payment.BillingDay.ToString();
        AddSaveButtons();
    }

    private bool IsDirectDebit => (string?)_method.SelectedItem == "Direct debit";

    protected override string EndorsementType => "Payment";

    protected override bool CheckInput(DateTime effective)
    {
        if (IsDirectDebit && !IsValidIban(_iban.Text))
        {
            Error(this, "IBAN is not valid.");
            return false;
        }
        if (IsDirectDebit && _holder.Text.Trim().Length < 2)
        {
            Error(this, "Direct debit mandate requires the account holder.");
            return false;
        }
        if (!int.TryParse(_day.Text.Trim(), out var day) || day is < 1 or > 28)
        {
            Error(this, "Billing day must be between 1 and 28.");
            return false;
        }
        return true;
    }

    protected override void Apply(Policy policy) => policy.Payment = new Payment
    {
        Method = (string)_method.SelectedItem!,
        Iban = IsDirectDebit ? _iban.Text.Trim().ToUpperInvariant() : "",
        AccountHolder = IsDirectDebit ? _holder.Text.Trim() : "",
        BillingDay = int.Parse(_day.Text.Trim()),
    };
}
