using System;
using System.IO;
using System.Xml.Linq;
using StaffOnboarding.Core.Access;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Tests;

public class AccessPlannerTests
{
    private static readonly AccessMap Map =
        AccessMap.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "access-map.xml"));

    private readonly AccessPlanner _planner = new(Map);

    private static PositionDetails Position(string dept, string role, EmploymentType type = EmploymentType.Permanent) =>
        new() { Department = dept, JobRole = role, ManagerAccount = "mikko.laine", EmploymentType = type };

    [Fact]
    public void Joiner_gets_baseline_department_and_role_groups()
    {
        var plan = _planner.PlanJoiner(Position("LOG", "LOG-PLANNER"), null);

        Assert.Equal("OU=Logistics,OU=Staff,OU=Users,OU=Litware,DC=corp,DC=local", plan.TargetOu);
        Assert.Equal(@"\\corp.local\dfs\dept\Logistics", plan.DepartmentShare);
        Assert.Contains("GG-All Staff", plan.AddGroups);
        Assert.Contains("GG-LOG-Staff", plan.AddGroups);
        Assert.Contains("APP-TMS-Planners", plan.AddGroups);
        Assert.False(plan.RequiresITApproval);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Privileged_role_requires_it_approval()
    {
        var plan = _planner.PlanJoiner(Position("FIN", "FIN-CTRL"), null);
        Assert.True(plan.RequiresITApproval);
        Assert.Contains("APP-ERP-GL-Post", plan.AddGroups);
    }

    [Fact]
    public void Contractor_goes_to_external_ou_with_external_baseline()
    {
        var plan = _planner.PlanJoiner(Position("LOG", "LOG-WH", EmploymentType.Contractor), null);
        Assert.Equal(Map.ExternalOu, plan.TargetOu);
        Assert.Contains("GG-External Staff", plan.AddGroups);
        Assert.DoesNotContain("GG-All Staff", plan.AddGroups);
    }

    [Fact]
    public void Requested_groups_must_be_requestable_and_need_it_approval()
    {
        var plan = _planner.PlanJoiner(Position("SAL", "SAL-KAM"), new[] { "APP-PowerBI-Pro", "Domain Admins" });
        Assert.Contains("APP-PowerBI-Pro", plan.AddGroups);
        Assert.DoesNotContain("Domain Admins", plan.AddGroups);
        Assert.True(plan.RequiresITApproval);
        Assert.Single(plan.Warnings);
    }

    [Fact]
    public void Unknown_role_grants_department_groups_and_needs_review()
    {
        var plan = _planner.PlanJoiner(Position("SAL", "SAL-NEW"), null);
        Assert.Contains("GG-SAL-Staff", plan.AddGroups);
        Assert.True(plan.RequiresITApproval);
        Assert.Single(plan.Warnings);
    }

    [Fact]
    public void Unknown_department_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => _planner.PlanJoiner(Position("XXX", "X"), null));
    }

    [Fact]
    public void Mover_adds_new_role_and_defers_removal_of_old_groups()
    {
        var current = new[] { "Domain Users", "GG-All Staff", "GG-Intranet Readers", "APP-Expenses-Users", "LIC-M365-E3",
            "GG-FIN-Staff", "FS-Finance-Read", "APP-ERP-AP-Entry", "FS-Finance-AP-Modify", "APP-Payroll-Readers", "Cricket Club" };

        var plan = _planner.PlanMover(current, Position("FIN", "FIN-AR"), null, 10);

        Assert.Equal(new[] { "APP-CreditDesk-Users", "APP-ERP-AR-Entry" }, plan.AddGroups);
        Assert.Equal(new[] { "APP-Payroll-Readers" }, plan.RemoveGroups);
        Assert.Equal(new[] { "APP-ERP-AP-Entry", "FS-Finance-AP-Modify" }, plan.DeferredRemoveGroups);
        Assert.False(plan.RequiresITApproval);
        Assert.Contains(plan.Warnings, w => w.Contains("Cricket Club"));
    }

    [Fact]
    public void Mover_without_handover_removes_immediately()
    {
        var plan = _planner.PlanMover(new[] { "GG-LOG-Staff", "APP-WMS-Handheld" }, Position("LOG", "LOG-PLANNER"), null, 0);
        Assert.Equal(new[] { "APP-WMS-Handheld" }, plan.RemoveGroups);
        Assert.Empty(plan.DeferredRemoveGroups);
    }

    [Fact]
    public void Mover_into_privileged_role_requires_it_approval()
    {
        var plan = _planner.PlanMover(new[] { "GG-ITS-Staff", "APP-ServiceDesk-Agents" }, Position("ITS", "ITS-SYS"), null, 5);
        Assert.True(plan.RequiresITApproval);
        Assert.Equal(new[] { "ROLE-Server Operators" }, plan.AddGroups.FindAll(g => g.StartsWith("ROLE-")));
    }

    [Fact]
    public void Leaver_removes_everything_but_primary_group()
    {
        var plan = _planner.PlanLeaver(new[] { "Domain Users", "GG-All Staff", "Cricket Club" });
        Assert.Equal(Map.DisabledOu, plan.TargetOu);
        Assert.Equal(new[] { "Cricket Club", "GG-All Staff" }, plan.RemoveGroups);
    }

    [Fact]
    public void Map_requires_root_element()
    {
        Assert.Throws<FormatException>(() => AccessMap.Parse(XDocument.Parse("<map />")));
    }
}
