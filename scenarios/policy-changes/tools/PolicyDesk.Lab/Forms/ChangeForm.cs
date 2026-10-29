namespace PolicyDesk.Lab.Forms;

/// <summary>Change dialog: effective date first, Save creates an endorsement.</summary>
public abstract class ChangeForm : LabForm
{
    protected ChangeForm(Policy policy, string user, string title)
    {
        Policy = policy;
        User = user;
        Text = $"PolicyDesk - {title} - {policy.Number}";
        EffectiveDate = AddText("Effective date:", 100);
        EffectiveDate.Text = DateTime.Today.ToString(DateFormat);
    }

    protected Policy Policy { get; }
    protected string User { get; }
    protected TextBox EffectiveDate { get; }

    protected void AddSaveButtons(params string[] extra)
    {
        AddButtons(extra.Concat(new[] { "Save", "Cancel" }).ToArray());
        ButtonByText("Save").Click += (_, _) => Save();
        ButtonByText("Cancel").Click += (_, _) => Close();
    }

    protected bool TryEffective(out DateTime effective)
    {
        if (!TryDate(EffectiveDate.Text, out effective))
        {
            Error(this, "Effective date is not a valid date (dd.mm.yyyy).");
            return false;
        }
        if (effective < Policy.StartDate.Date)
        {
            Error(this, $"Effective date is before the policy start date {Policy.StartDate.ToString(DateFormat)}.");
            return false;
        }
        if (effective > DateTime.Today.AddDays(120))
        {
            Error(this, "Effective date more than 120 days ahead is not allowed.");
            return false;
        }
        return true;
    }

    private void Save()
    {
        if (!TryEffective(out var effective) || !CheckInput(effective)) return;
        if (!Confirm()) return;
        var endorsement = LabData.Endorse(Policy.Number, effective, EndorsementType, User, Apply);
        Info(this, EndorsementMessage(endorsement));
        Close();
    }

    protected abstract string EndorsementType { get; }
    protected abstract bool CheckInput(DateTime effective);
    protected abstract void Apply(Policy policy);
    protected virtual bool Confirm() => true;
    protected virtual string EndorsementMessage(string number) => $"Endorsement {number} created.";

    protected static bool IsValidIban(string iban)
    {
        iban = new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (iban.Length < 15 || iban.Length > 34 || !iban.All(char.IsAsciiLetterOrDigit)) return false;
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var digits = char.IsAsciiDigit(c) ? (c - '0').ToString() : (c - 'A' + 10).ToString();
            foreach (var d in digits) remainder = (remainder * 10 + (d - '0')) % 97;
        }
        return remainder == 1;
    }
}
