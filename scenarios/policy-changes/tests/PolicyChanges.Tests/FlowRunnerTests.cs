using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Requests;
using PolicyChanges.Tests.Fakes;

namespace PolicyChanges.Tests;

public class FlowRunnerTests
{
    private static FlowDefinition Flow(ChangeType type) =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "flows"), "*.json")
            .Select(f => FlowDefinition.Parse(File.ReadAllText(f)))
            .Single(f => f.ChangeType == type);

    private static FlowRequest Request(string policy, ChangeType type, params (string, string)[] fields) =>
        new("PC-261029-0001", policy, type, new DateOnly(2026, 11, 1), fields.ToDictionary(f => f.Item1, f => f.Item2));

    private static (FakeDesk Desk, FakePolicy Policy) Desk(string number = "HL-1042118", bool life = false)
    {
        var desk = new FakeDesk();
        var policy = new FakePolicy { Address = "Old street 1, 33100 TAMPERE, FI", Life = life };
        desk.Policies[number] = policy;
        return (desk, policy);
    }

    [Fact]
    public void Address_change_creates_endorsement_and_verifies_address()
    {
        var (desk, policy) = Desk();
        var result = new FlowRunner(desk).Run(Flow(ChangeType.AddressChange), Request("HL-1042118", ChangeType.AddressChange,
            ("Street", "Hämeenkatu 12"), ("PostalCode", "33100"), ("City", "Tampere"), ("Country", "FI")));

        Assert.Equal(FlowOutcome.Applied, result.Outcome);
        Assert.Equal("6004101", result.Endorsement);
        Assert.Equal("Hämeenkatu 12, 33100 TAMPERE, FI", policy.Address);
        Assert.Contains("Effective date=01.11.2026", desk.Actions);
        Assert.Equal(new[] { "policy-before", "address-entered", "endorsement", "policy-after" }, result.Evidence.Select(e => e.Name));
        Assert.Single(desk.Windows); // back on the main window
    }

    [Fact]
    public void Unknown_policy_is_rejected()
    {
        var (desk, _) = Desk();
        var result = new FlowRunner(desk).Run(Flow(ChangeType.AddressChange), Request("HL-2056331", ChangeType.AddressChange,
            ("Street", "Street 1"), ("PostalCode", "33100"), ("City", "Tampere"), ("Country", "FI")));

        Assert.Equal(FlowOutcome.Rejected, result.Outcome);
        Assert.Equal("Policy HL-2056331 was not found.", result.Message);
    }

    [Fact]
    public void Policy_not_in_force_is_rejected()
    {
        var (desk, policy) = Desk();
        policy.Status = "Cancelled";
        var result = new FlowRunner(desk).Run(Flow(ChangeType.PaymentMethodChange), Request("HL-1042118", ChangeType.PaymentMethodChange,
            ("PaymentMethod", "Invoice"), ("BillingDay", "15")));

        Assert.Equal(FlowOutcome.Rejected, result.Outcome);
        Assert.Contains("not in force", result.Message);
    }

    [Fact]
    public void Direct_debit_enters_formatted_iban()
    {
        var (desk, policy) = Desk();
        var result = new FlowRunner(desk).Run(Flow(ChangeType.PaymentMethodChange), Request("HL-1042118", ChangeType.PaymentMethodChange,
            ("PaymentMethod", "DirectDebit"), ("Iban", "FI2112345600000785"), ("AccountHolder", "Aino Virtanen"), ("BillingDay", "5")));

        Assert.Equal(FlowOutcome.Applied, result.Outcome);
        Assert.Contains("IBAN=FI21 1234 5600 0007 85", desk.Actions);
        Assert.Equal("Direct debit FI21 1234 5600 0007 85", policy.Payment);
    }

    [Fact]
    public void Invoice_skips_bank_fields()
    {
        var (desk, _) = Desk();
        var result = new FlowRunner(desk).Run(Flow(ChangeType.PaymentMethodChange), Request("HL-1042118", ChangeType.PaymentMethodChange,
            ("PaymentMethod", "Invoice"), ("BillingDay", "20")));

        Assert.Equal(FlowOutcome.Applied, result.Outcome);
        Assert.DoesNotContain(desk.Actions, a => a.StartsWith("IBAN="));
    }

    [Fact]
    public void Beneficiaries_are_replaced()
    {
        var (desk, policy) = Desk("HL-2056331", life: true);
        policy.Beneficiaries.Add(new[] { "Karin Lindqvist", "14.03.1979", "100" });
        var result = new FlowRunner(desk).Run(Flow(ChangeType.BeneficiaryChange), Request("HL-2056331", ChangeType.BeneficiaryChange,
            ("Beneficiaries", "Anna Lindqvist|2001-02-03|50\nOlle Lindqvist|2004-05-06|50")));

        Assert.Equal(FlowOutcome.Applied, result.Outcome);
        Assert.Equal(new[] { "Anna Lindqvist", "Olle Lindqvist" }, policy.Beneficiaries.Select(b => b[0]));
        Assert.Equal("03.02.2001", policy.Beneficiaries[0][1]);
        Assert.Equal("2", result.Variables["Rows"]);
    }

    [Fact]
    public void Beneficiaries_on_non_life_policy_are_rejected()
    {
        var (desk, _) = Desk();
        var result = new FlowRunner(desk).Run(Flow(ChangeType.BeneficiaryChange), Request("HL-1042118", ChangeType.BeneficiaryChange,
            ("Beneficiaries", "Anna|2001-02-03|100")));

        Assert.Equal(FlowOutcome.Rejected, result.Outcome);
        Assert.Contains("life policies", result.Message);
    }

    [Fact]
    public void Cancellation_confirms_and_reads_refund()
    {
        var (desk, policy) = Desk();
        var result = new FlowRunner(desk).Run(Flow(ChangeType.Cancellation), Request("HL-1042118", ChangeType.Cancellation,
            ("Reason", "CustomerRequest")));

        Assert.Equal(FlowOutcome.Applied, result.Outcome);
        Assert.Equal("Cancelled", policy.Status);
        Assert.Equal("123,45 EUR", result.Variables["Refund"]);
        Assert.Contains("Reason=Customer request", desk.Actions);
    }

    [Fact]
    public void Screen_change_fails_with_step_and_captures_screen()
    {
        var (desk, _) = Desk();
        var flow = Flow(ChangeType.AddressChange);
        flow.Steps.Single(s => s.Field == "Street").Field = "Street address";
        var result = new FlowRunner(desk).Run(flow, Request("HL-1042118", ChangeType.AddressChange,
            ("Street", "Street 1"), ("PostalCode", "33100"), ("City", "Tampere"), ("Country", "FI")));

        Assert.Equal(FlowOutcome.Failed, result.Outcome);
        Assert.StartsWith("Step 11 Set: No input labelled 'Street address'", result.Message);
        Assert.Equal("failed", result.Evidence.Last().Name);
    }

    [Fact]
    public void Unexpected_message_fails()
    {
        var (desk, _) = Desk();
        var flow = Flow(ChangeType.AddressChange);
        flow.Steps.Single(s => s.Name == "endorsement").Reject!.Clear();
        var result = new FlowRunner(desk).Run(flow, Request("HL-1042118", ChangeType.AddressChange,
            ("Street", "Street 1"), ("PostalCode", "3310"), ("City", "Tampere"), ("Country", "FI")));

        Assert.Equal(FlowOutcome.Failed, result.Outcome);
        Assert.Contains("Unexpected PolicyDesk message: Postal code 3310 is not valid for FI.", result.Message);
    }
}
