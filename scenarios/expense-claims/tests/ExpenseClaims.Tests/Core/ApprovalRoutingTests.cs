using ExpenseClaims.Core.Policy;

namespace ExpenseClaims.Tests.Core;

public class ApprovalRoutingTests
{
    private static readonly Dictionary<string, string?> Managers = new()
    {
        ["100233"] = "100150",
        ["100150"] = "100102",
        ["100102"] = "100001",
        ["100001"] = null
    };

    private static readonly ExpensePolicyOptions Options = new() { SecondApprovalFrom = 2500m };

    private static string? ManagerOf(string n) => Managers.TryGetValue(n, out var m) ? m : null;

    [Fact]
    public void Small_claim_goes_to_line_manager()
    {
        var steps = ApprovalRouting.Build("100233", 400m, Options, ManagerOf);
        Assert.Equal(new[] { "100150" }, steps.Select(s => s.ApproverEmployeeNumber));
    }

    [Fact]
    public void Large_claim_also_needs_managers_manager()
    {
        var steps = ApprovalRouting.Build("100233", 2500m, Options, ManagerOf);
        Assert.Equal(new[] { (1, "100150"), (2, "100102") }, steps.Select(s => (s.Level, s.ApproverEmployeeNumber)));
    }

    [Fact]
    public void Top_of_organisation_uses_fallback_approver()
    {
        Assert.Empty(ApprovalRouting.Build("100001", 100m, Options, ManagerOf));
        var steps = ApprovalRouting.Build("100001", 100m, Options, ManagerOf, fallbackApprover: "100014");
        Assert.Equal("100014", Assert.Single(steps).ApproverEmployeeNumber);
    }

    [Fact]
    public void Fallback_is_not_used_for_own_claim() =>
        Assert.Empty(ApprovalRouting.Build("100001", 100m, Options, ManagerOf, fallbackApprover: "100001"));

    [Fact]
    public void Manager_loop_stops()
    {
        var loop = new Dictionary<string, string?> { ["A"] = "B", ["B"] = "A" };
        var steps = ApprovalRouting.Build("A", 5000m, Options, n => loop[n]);
        Assert.Equal("B", Assert.Single(steps).ApproverEmployeeNumber);
    }
}
