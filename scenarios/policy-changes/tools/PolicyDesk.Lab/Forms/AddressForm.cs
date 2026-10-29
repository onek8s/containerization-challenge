using System.Text.RegularExpressions;

namespace PolicyDesk.Lab.Forms;

public sealed class AddressForm : ChangeForm
{
    private readonly TextBox _street, _postal, _city;
    private readonly ComboBox _country;

    public AddressForm(Policy policy, string user) : base(policy, user, "Change address")
    {
        _street = AddText("Street:");
        _postal = AddText("Postal code:", 100);
        _city = AddText("City:", 200);
        _country = AddList("Country:", "FI", "SE", "NO", "EE");
        _street.Text = policy.Address.Street;
        _postal.Text = policy.Address.PostalCode;
        _city.Text = policy.Address.City;
        _country.SelectedItem = policy.Address.Country;
        AddSaveButtons();
    }

    protected override string EndorsementType => "Address";

    protected override bool CheckInput(DateTime effective)
    {
        var country = (string?)_country.SelectedItem ?? "";
        var pattern = country switch { "SE" => @"^\d{3} ?\d{2}$", "NO" => @"^\d{4}$", _ => @"^\d{5}$" };
        if (_street.Text.Trim().Length < 3 || _city.Text.Trim().Length < 2)
        {
            Error(this, "Street and city are required.");
            return false;
        }
        if (!Regex.IsMatch(_postal.Text.Trim(), pattern))
        {
            Error(this, $"Postal code {_postal.Text.Trim()} is not valid for {country}.");
            return false;
        }
        return true;
    }

    protected override void Apply(Policy policy) => policy.Address = new Address
    {
        Street = _street.Text.Trim(),
        PostalCode = _postal.Text.Trim(),
        City = _city.Text.Trim().ToUpperInvariant(),
        Country = (string)_country.SelectedItem!,
    };
}
