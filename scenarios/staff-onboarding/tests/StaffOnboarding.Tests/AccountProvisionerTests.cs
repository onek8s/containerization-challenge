using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Provisioning;
using StaffOnboarding.Provisioning.Provisioning;

namespace StaffOnboarding.Tests;

public class AccountProvisionerTests
{
    private const string StaffOu = "OU=Logistics,OU=Staff,OU=Users,OU=Litware,DC=corp,DC=local";
    private const string DisabledOu = "OU=Disabled,OU=Users,OU=Litware,DC=corp,DC=local";

    private readonly FakeDirectory _directory = new();
    private readonly FakeHomeFolders _home = new();
    private readonly FakeJournal _journal = new();
    private readonly AccountProvisioner _provisioner;

    public AccountProvisionerTests()
    {
        _directory.ExistingGroups.UnionWith(new[] { "GG-All Staff", "GG-LOG-Staff", "APP-TMS-Planners", "APP-WMS-Handheld" });
        _directory.AddExisting("mikko.laine", StaffOu);
        _provisioner = new AccountProvisioner(_directory, _home, _journal,
            Options.Create(new ProvisioningOptions()), NullLogger<AccountProvisioner>.Instance, () => "Initial-Pa55word!");
    }

    private static CreateAccountCommand Joiner(string request = "ONB-2026-00042") => new()
    {
        RequestNumber = request,
        Step = "create",
        FirstName = "Anna",
        LastName = "Järvinen",
        EmployeeId = "100231",
        Department = "LOG",
        JobRole = "LOG-PLANNER",
        Title = "Transport Planner",
        ManagerAccount = "mikko.laine",
        EmploymentType = "Permanent",
        TargetOu = StaffOu,
        Groups = { "GG-All Staff", "GG-LOG-Staff", "APP-TMS-Planners", "APP-Missing" },
        CreateDisabled = true
    };

    [Fact]
    public void Create_makes_disabled_account_with_groups_and_home_folder()
    {
        var result = _provisioner.Create(Joiner(), @"CORP\svc-onbflow$");

        Assert.True(result.Succeeded);
        Assert.Equal("anna.jarvinen", result.SamAccountName);
        Assert.Equal("anna.jarvinen@litware.com", result.Mail);
        Assert.Equal(@"\\corp.local\dfs\home\anna.jarvinen", result.HomeDirectory);
        Assert.Equal("dc01.corp.local", result.DomainController);
        Assert.Contains(result.Messages, m => m.Contains("APP-Missing"));

        var user = _directory.Users["anna.jarvinen"];
        Assert.False(user.Enabled);
        Assert.Equal("Initial-Pa55word!", user.Password);
        Assert.Equal($"CN=mikko.laine,{StaffOu}", user.Created.ManagerDn);
        Assert.Equal("Onboarding ONB-2026-00042", user.Created.Description);
        Assert.Contains("APP-TMS-Planners", user.Groups);
        Assert.Equal("H:", user.Attributes["homeDrive"]);
        Assert.Equal(user.Sid, _home.Created["anna.jarvinen"]);
    }

    [Fact]
    public void Repeated_create_returns_journal_result()
    {
        var first = _provisioner.Create(Joiner(), "caller");
        var second = _provisioner.Create(Joiner(), "caller");

        Assert.True(second.AlreadyDone);
        Assert.Equal(first.SamAccountName, second.SamAccountName);
        Assert.Equal(2, _directory.Users.Count);
    }

    [Fact]
    public void Transient_failure_is_retryable_and_retry_continues_with_same_account()
    {
        _home.FailCreate = new IOException("The network path was not found.") { HResult = unchecked((int)0x80070035) };

        var first = _provisioner.Create(Joiner(), "caller");
        Assert.False(first.Succeeded);
        Assert.True(first.Retryable);

        var second = _provisioner.Create(Joiner(), "caller");
        Assert.True(second.Succeeded);
        Assert.Equal("anna.jarvinen", second.SamAccountName);
        Assert.Contains(second.Messages, m => m.Contains("created earlier"));
        Assert.Equal(2, _directory.Users.Count);
    }

    [Fact]
    public void Directory_server_down_is_retryable()
    {
        _directory.FailNextAddToGroup = FakeDirectory.ServerDown();
        var result = _provisioner.Create(Joiner(), "caller");
        Assert.True(result.Retryable);
    }

    [Fact]
    public void Other_failures_are_not_retryable()
    {
        _directory.FailNextAddToGroup = new UnauthorizedAccessException("Access is denied.");
        var result = _provisioner.Create(Joiner(), "caller");
        Assert.False(result.Succeeded);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Name_collision_uses_next_candidate()
    {
        _directory.TakenNames.Add("anna.jarvinen");
        var result = _provisioner.Create(Joiner(), "caller");
        Assert.Equal("a.jarvinen", result.SamAccountName);
    }

    [Fact]
    public void Missing_step_is_rejected()
    {
        var command = Joiner();
        command.Step = null;
        Assert.False(_provisioner.Create(command, "caller").Succeeded);
    }

    [Fact]
    public void Update_access_moves_changes_groups_and_manager()
    {
        _directory.AddExisting("lena.berg", StaffOu, "mikko.laine", "GG-LOG-Staff", "APP-WMS-Handheld");
        _directory.AddExisting("sari.koski", StaffOu);

        var result = _provisioner.UpdateAccess(new UpdateAccessCommand
        {
            RequestNumber = "MOV-2026-00003",
            Step = "apply",
            SamAccountName = "lena.berg",
            Title = "Transport Planner",
            ManagerAccount = "sari.koski",
            TargetOu = "OU=Sales,OU=Staff,OU=Users,OU=Litware,DC=corp,DC=local",
            AddGroups = { "APP-TMS-Planners" },
            RemoveGroups = { "APP-WMS-Handheld" }
        }, "caller");

        Assert.True(result.Succeeded);
        var user = _directory.Users["lena.berg"];
        Assert.Contains("APP-TMS-Planners", user.Groups);
        Assert.DoesNotContain("APP-WMS-Handheld", user.Groups);
        Assert.Equal($"CN=sari.koski,{StaffOu}", user.Attributes["manager"]);
        Assert.StartsWith("CN=lena.berg,OU=Sales", user.Dn);
    }

    [Fact]
    public void Disable_removes_groups_and_moves_to_disabled_ou()
    {
        _directory.AddExisting("lena.berg", StaffOu, "mikko.laine", "GG-LOG-Staff");
        _directory.UpdateAttributes("lena.berg", new Dictionary<string, object?> { ["homeDirectory"] = @"\\corp.local\dfs\home\lena.berg" });

        var result = _provisioner.Disable(new DisableAccountCommand
        {
            RequestNumber = "LVR-2026-00011", Step = "disable", SamAccountName = "lena.berg",
            TargetOu = DisabledOu, RemoveGroups = { "GG-LOG-Staff" }, Reason = "Leaver LVR-2026-00011"
        }, "caller");

        Assert.True(result.Succeeded);
        var user = _directory.Users["lena.berg"];
        Assert.False(user.Enabled);
        Assert.Equal(new[] { "Domain Users" }, user.Groups);
        Assert.EndsWith(DisabledOu, user.Dn);
        Assert.StartsWith("Disabled ", (string)user.Attributes["description"]!);
        Assert.Equal(@"\\corp.local\dfs\home\lena.berg", result.HomeDirectory);
    }

    [Fact]
    public void Archive_moves_home_folder_and_clears_attributes()
    {
        _directory.AddExisting("lena.berg", DisabledOu);
        _directory.UpdateAttributes("lena.berg", new Dictionary<string, object?> { ["homeDirectory"] = @"\\corp.local\dfs\home\lena.berg" });

        var result = _provisioner.Archive(new ArchiveAccountCommand
        {
            RequestNumber = "LVR-2026-00011", Step = "archive", SamAccountName = "lena.berg", HandoverTo = "mikko.laine"
        }, "caller");

        Assert.True(result.Succeeded);
        Assert.Equal(@"\\corp.local\dfs\archive\home\2026\lena.berg", result.HomeDirectory);
        Assert.Equal(("lena.berg", @"\\corp.local\dfs\home\lena.berg", "mikko.laine"), _home.Archived.Single());
        Assert.Null(_directory.Users["lena.berg"].Attributes["homeDirectory"]);
    }

    [Fact]
    public void Get_returns_summary_with_manager_account_and_groups()
    {
        _directory.AddExisting("lena.berg", StaffOu, "mikko.laine", "GG-LOG-Staff");
        var summary = _provisioner.Get("lena.berg");
        Assert.NotNull(summary);
        Assert.Equal("mikko.laine", summary!.ManagerAccount);
        Assert.Equal(new[] { "Domain Users", "GG-LOG-Staff" }, summary.Groups);
        Assert.Null(_provisioner.Get("nobody"));
    }

    [Fact]
    public void Unknown_account_fails_permanently()
    {
        var result = _provisioner.Enable(new EnableAccountCommand { RequestNumber = "ONB-2026-00001", Step = "enable", SamAccountName = "ghost" }, "caller");
        Assert.False(result.Succeeded);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Generated_passwords_meet_complexity()
    {
        for (var i = 0; i < 50; i++)
        {
            var p = PasswordGenerator.Create();
            Assert.Equal(24, p.Length);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, c => !char.IsLetterOrDigit(c));
        }
    }
}
