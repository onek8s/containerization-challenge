using Microsoft.Extensions.Time.Testing;
using PolicyChanges.Core.Requests;

namespace PolicyChanges.Tests;

public class PolicyNumberTests
{
    [Theory]
    [InlineData("HL-1042118", true)]
    [InlineData("hl1042118", true)]
    [InlineData("HL-1042119", false)]
    [InlineData("HL-104211", false)]
    [InlineData("XX-1042118", false)]
    public void Validates_check_digit(string number, bool valid) => Assert.Equal(valid, PolicyNumber.IsValid(number));

    [Fact]
    public void Normalizes_and_creates() 
    {
        Assert.Equal("HL-1042118", PolicyNumber.Normalize(" hl 1042118 "));
        Assert.Equal("HL-2056331", PolicyNumber.Create(205633));
    }
}

public class IbanTests
{
    [Theory]
    [InlineData("FI21 1234 5600 0007 85", true)]
    [InlineData("SE4550000000058398257466", true)]
    [InlineData("FI21 1234 5600 0007 86", false)]
    [InlineData("FI21 1234 5600 0007", false)]
    [InlineData("XX21 1234 5600 0007 85", false)]
    public void Validates(string iban, bool valid) => Assert.Equal(valid, Iban.IsValid(iban));

    [Fact]
    public void Formats_in_groups_of_four() => Assert.Equal("FI21 1234 5600 0007 85", Iban.Format("fi2112345600000785"));
}

public class ChangeValidatorTests
{
    private readonly ChangeValidator _validator = new(new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero)));
    private static readonly DateOnly Today = new(2026, 10, 29);

    private static ChangeDraft Draft(ChangeType type, params (string, string)[] fields) => new()
    {
        PolicyNumber = "HL-1042118",
        ChangeType = type,
        EffectiveDate = Today,
        Fields = fields.ToDictionary(f => f.Item1, f => f.Item2, StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void Accepts_valid_address() =>
        Assert.Empty(_validator.Validate(Draft(ChangeType.AddressChange,
            ("Street", "Hämeenkatu 1"), ("PostalCode", "33100"), ("City", "Tampere"), ("Country", "FI"))));

    [Theory]
    [InlineData("FI", "3310")]
    [InlineData("SE", "4113")]
    [InlineData("NO", "12345")]
    public void Rejects_postal_code_for_country(string country, string postal) =>
        Assert.Contains(_validator.Validate(Draft(ChangeType.AddressChange,
            ("Street", "Street 1"), ("PostalCode", postal), ("City", "City"), ("Country", country))), e => e.Contains("Postal code"));

    [Fact]
    public void Rejects_effective_date_out_of_range()
    {
        var draft = Draft(ChangeType.AddressChange, ("Street", "Street 1"), ("PostalCode", "00100"), ("City", "Helsinki"), ("Country", "FI"));
        draft.EffectiveDate = Today.AddDays(-31);
        Assert.Contains(_validator.Validate(draft), e => e.Contains("days back"));
        draft.EffectiveDate = Today.AddDays(91);
        Assert.Contains(_validator.Validate(draft), e => e.Contains("days ahead"));
    }

    [Fact]
    public void Direct_debit_needs_valid_iban_and_holder()
    {
        var errors = _validator.Validate(Draft(ChangeType.PaymentMethodChange, ("PaymentMethod", "DirectDebit"), ("Iban", "FI00 1234"), ("BillingDay", "15")));
        Assert.Contains("IBAN is not valid.", errors);
        Assert.Contains(errors, e => e.StartsWith("Account holder"));
    }

    [Fact]
    public void Invoice_must_not_have_iban() =>
        Assert.Contains("IBAN is only given for direct debit.",
            _validator.Validate(Draft(ChangeType.PaymentMethodChange, ("PaymentMethod", "Invoice"), ("Iban", "FI2112345600000785"), ("BillingDay", "1"))));

    [Fact]
    public void Beneficiary_shares_must_total_100()
    {
        Assert.Empty(_validator.Validate(Draft(ChangeType.BeneficiaryChange, ("Beneficiaries", "Anna Berg|1990-01-02|60\nOlle Berg|1992-05-06|40"))));
        Assert.Contains("Beneficiary shares must total 100.",
            _validator.Validate(Draft(ChangeType.BeneficiaryChange, ("Beneficiaries", "Anna Berg|1990-01-02|60\nOlle Berg|1992-05-06|30"))));
        Assert.Contains(_validator.Validate(Draft(ChangeType.BeneficiaryChange, ("Beneficiaries", "Anna Berg;1990-01-02;100"))),
            e => e.Contains("expected name|yyyy-MM-dd|share"));
    }

    [Fact]
    public void Backdated_cancellation_only_for_death_or_sold_vehicle()
    {
        var draft = Draft(ChangeType.Cancellation, ("Reason", "CustomerRequest"));
        draft.EffectiveDate = Today.AddDays(-3);
        Assert.Contains(_validator.Validate(draft), e => e.StartsWith("Backdated cancellation"));
        draft.Fields["Reason"] = "VehicleSold";
        Assert.Empty(_validator.Validate(draft));
    }

    [Fact]
    public void Rejects_fields_of_other_change_types() =>
        Assert.Contains("Field Iban is not used for AddressChange.",
            _validator.Validate(Draft(ChangeType.AddressChange, ("Street", "Street 1"), ("PostalCode", "00100"), ("City", "Helsinki"), ("Country", "FI"), ("Iban", "x"))));

    [Fact]
    public void References_count_per_day()
    {
        Assert.Equal("PC-261029-0001", ChangeReference.Create(Today, ChangeReference.NextSequence(null)));
        Assert.Equal(13, ChangeReference.NextSequence("PC-261029-0012"));
    }
}
