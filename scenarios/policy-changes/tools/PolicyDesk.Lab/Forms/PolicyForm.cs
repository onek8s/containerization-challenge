namespace PolicyDesk.Lab.Forms;

public sealed class PolicyForm : LabForm
{
    private readonly string _number;
    private readonly string _user;
    private readonly TextBox _holder, _product, _status, _address, _payment, _premium;
    private readonly ListView _endorsements;

    public PolicyForm(string number, string user)
    {
        _number = number;
        _user = user;
        Text = $"PolicyDesk - Policy {number}";
        _holder = AddText("Policyholder:", readOnly: true);
        _product = AddText("Product:", readOnly: true);
        _status = AddText("Status:", 160, readOnly: true);
        _address = AddText("Address:", 350, readOnly: true);
        _payment = AddText("Payment:", 350, readOnly: true);
        _premium = AddText("Annual premium:", 160, readOnly: true);
        _endorsements = AddGrid("Endorsements:", 140, ("No", 70), ("Effective", 80), ("Type", 110), ("Entered by", 90));
        AddButtons("Change address...", "Change payment...", "Beneficiaries...", "Cancel policy...");
        AddButtons("Close");

        ButtonByText("Change address...").Click += (_, _) => Change(p => new AddressForm(p, _user));
        ButtonByText("Change payment...").Click += (_, _) => Change(p => new PaymentForm(p, _user));
        ButtonByText("Beneficiaries...").Click += (_, _) => Change(p =>
        {
            if (p.IsLife) return new BeneficiariesForm(p, _user);
            Error(this, "Beneficiaries can only be named on life policies.");
            return null;
        });
        ButtonByText("Cancel policy...").Click += (_, _) => Change(p => new CancelForm(p, _user));
        ButtonByText("Close").Click += (_, _) => Close();
        ShowPolicy(LabData.Load().Find(number)!);
    }

    private void Change(Func<Policy, Form?> create)
    {
        var policy = LabData.Load().Find(_number)!;
        if (!policy.InForce)
        {
            Error(this, $"Policy {_number} is not in force ({policy.Status}). Changes are not allowed.");
            return;
        }
        using var form = create(policy);
        if (form is null) return;
        form.ShowDialog(this);
        ShowPolicy(LabData.Load().Find(_number)!);
    }

    private void ShowPolicy(Policy p)
    {
        _holder.Text = p.Holder;
        _product.Text = p.Product;
        _status.Text = p.Status;
        _address.Text = p.Address.ToString();
        _payment.Text = p.Payment.ToString();
        _premium.Text = p.AnnualPremium.ToString("N2") + " EUR";
        _endorsements.Items.Clear();
        foreach (var e in p.Endorsements.OrderByDescending(e => e.Number))
            _endorsements.Items.Add(new ListViewItem(new[] { e.Number, e.Effective.ToString(DateFormat), e.Type, e.EnteredBy }));
    }
}
