using Microsoft.Extensions.Options;
using StaffOnboarding.Core.Naming;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Provisioning.Directory;
using StaffOnboarding.Provisioning.HomeFolders;
using StaffOnboarding.Provisioning.Operations;

namespace StaffOnboarding.Provisioning.Provisioning;

/// <summary>Carries out provisioning commands. Every step can be repeated safely.</summary>
public sealed class AccountProvisioner
{
    private readonly IDirectoryService _directory;
    private readonly IHomeFolderService _homeFolders;
    private readonly IOperationJournal _journal;
    private readonly ProvisioningOptions _options;
    private readonly AccountNaming _naming;
    private readonly ILogger<AccountProvisioner> _logger;
    private readonly Func<string> _passwords;

    public AccountProvisioner(IDirectoryService directory, IHomeFolderService homeFolders, IOperationJournal journal,
        IOptions<ProvisioningOptions> options, ILogger<AccountProvisioner> logger)
        : this(directory, homeFolders, journal, options, logger, () => PasswordGenerator.Create())
    {
    }

    internal AccountProvisioner(IDirectoryService directory, IHomeFolderService homeFolders, IOperationJournal journal,
        IOptions<ProvisioningOptions> options, ILogger<AccountProvisioner> logger, Func<string> passwords)
    {
        _directory = directory;
        _homeFolders = homeFolders;
        _journal = journal;
        _options = options.Value;
        _logger = logger;
        _passwords = passwords;
        _naming = new AccountNaming(_options.UpnSuffix, _options.MailDomain);
    }

    public ProvisioningResult Create(CreateAccountCommand c, string caller) => Run(c, caller, null, result =>
    {
        var existing = _directory.FindCreatedFor(c.RequestNumber!);
        string sam;
        if (existing != null)
        {
            sam = existing.SamAccountName;
            result.Messages.Add($"Continuing with {sam} created earlier for this request.");
        }
        else
        {
            var person = new PersonDetails
            {
                FirstName = c.FirstName, LastName = c.LastName, PreferredName = c.PreferredName, EmployeeId = c.EmployeeId
            };
            var type = Enum.TryParse<EmploymentType>(c.EmploymentType, true, out var t) ? t : EmploymentType.Permanent;
            var name = _naming.Generate(person, type, candidate =>
                _directory.NameInUse(candidate, _naming.UpnFor(candidate), _naming.MailFor(candidate)));

            var managerDn = string.IsNullOrWhiteSpace(c.ManagerAccount) ? null : _directory.FindUser(c.ManagerAccount)?.DistinguishedName;
            if (managerDn == null && !string.IsNullOrWhiteSpace(c.ManagerAccount))
                result.Messages.Add($"Manager {c.ManagerAccount} not found; manager not set.");

            _directory.CreateUser(new NewUser
            {
                SamAccountName = name.SamAccountName,
                UserPrincipalName = name.UserPrincipalName,
                Mail = name.Mail,
                GivenName = person.DisplayFirstName,
                Surname = c.LastName!,
                DisplayName = person.DisplayName,
                Ou = c.TargetOu!,
                EmployeeId = c.EmployeeId,
                EmployeeType = type.ToString(),
                Department = c.Department,
                Title = c.Title,
                Office = c.Office,
                CostCenter = c.CostCenter,
                Company = _options.Company,
                MobilePhone = c.MobilePhone,
                ManagerDn = managerDn,
                AccountExpiresUtc = c.AccountExpiresUtc,
                Description = "Onboarding " + c.RequestNumber
            }, _passwords());
            sam = name.SamAccountName;
        }

        AddGroups(sam, c.Groups, result);

        var account = _directory.FindUser(sam) ?? throw new InvalidOperationException($"{sam} not found after create.");
        var home = _homeFolders.Create(sam, account.Sid);
        _directory.UpdateAttributes(sam, new Dictionary<string, object?>
        {
            ["homeDirectory"] = home,
            ["homeDrive"] = _options.HomeDrive
        });

        if (!c.CreateDisabled) _directory.SetEnabled(sam, true);

        Fill(result, _directory.FindUser(sam)!);
        result.HomeDirectory = home;
        return sam;
    });

    public ProvisioningResult Enable(EnableAccountCommand c, string caller) => Run(c, caller, c.SamAccountName, result =>
    {
        var account = Require(c.SamAccountName!);
        _directory.SetEnabled(account.SamAccountName, true);
        Fill(result, account);
        return account.SamAccountName;
    });

    public ProvisioningResult UpdateAccess(UpdateAccessCommand c, string caller) => Run(c, caller, c.SamAccountName, result =>
    {
        var account = Require(c.SamAccountName!);
        var sam = account.SamAccountName;

        var attributes = new Dictionary<string, object?>();
        if (c.Department != null) attributes["department"] = c.Department;
        if (c.Title != null) attributes["title"] = c.Title;
        if (c.Office != null) attributes["physicalDeliveryOfficeName"] = c.Office;
        if (c.CostCenter != null) attributes["extensionAttribute1"] = c.CostCenter;
        if (c.AccountExpiresUtc.HasValue) attributes["accountExpires"] = c.AccountExpiresUtc.Value;
        if (!string.IsNullOrWhiteSpace(c.ManagerAccount))
        {
            var managerDn = _directory.FindUser(c.ManagerAccount)?.DistinguishedName;
            if (managerDn != null) attributes["manager"] = managerDn;
            else result.Messages.Add($"Manager {c.ManagerAccount} not found; manager not changed.");
        }
        if (attributes.Count > 0) _directory.UpdateAttributes(sam, attributes);

        if (!string.IsNullOrWhiteSpace(c.TargetOu)) _directory.MoveTo(sam, c.TargetOu);

        AddGroups(sam, c.AddGroups, result);
        foreach (var group in c.RemoveGroups) _directory.RemoveFromGroup(sam, group);

        Fill(result, _directory.FindUser(sam)!);
        return sam;
    });

    public ProvisioningResult Disable(DisableAccountCommand c, string caller) => Run(c, caller, c.SamAccountName, result =>
    {
        var account = Require(c.SamAccountName!);
        var sam = account.SamAccountName;

        _directory.SetEnabled(sam, false);
        _directory.UpdateAttributes(sam, new Dictionary<string, object?>
        {
            ["description"] = string.Format(System.Globalization.CultureInfo.InvariantCulture, _options.DisabledDescriptionFormat, DateTime.Today, c.RequestNumber),
            ["info"] = c.Reason
        });
        foreach (var group in c.RemoveGroups) _directory.RemoveFromGroup(sam, group);
        if (!string.IsNullOrWhiteSpace(c.TargetOu)) _directory.MoveTo(sam, c.TargetOu);

        Fill(result, _directory.FindUser(sam)!);
        return sam;
    });

    public ProvisioningResult Archive(ArchiveAccountCommand c, string caller) => Run(c, caller, c.SamAccountName, result =>
    {
        var account = Require(c.SamAccountName!);
        var sam = account.SamAccountName;
        var home = account.HomeDirectory ?? Path.Combine(_options.HomeRoot, sam);

        var archived = _homeFolders.Archive(sam, home, c.HandoverTo);
        _directory.UpdateAttributes(sam, new Dictionary<string, object?>
        {
            ["homeDirectory"] = null,
            ["homeDrive"] = null,
            ["info"] = "Home folder archived to " + archived
        });

        Fill(result, account);
        result.HomeDirectory = archived;
        return sam;
    });

    public AccountSummary? Get(string samAccountName)
    {
        var account = _directory.FindUser(samAccountName);
        if (account == null) return null;
        var summary = new AccountSummary
        {
            SamAccountName = account.SamAccountName,
            DisplayName = account.DisplayName,
            Mail = account.Mail,
            DistinguishedName = account.DistinguishedName,
            Department = account.Department,
            Title = account.Title,
            ManagerAccount = account.ManagerDn == null ? null : _directory.GetSamAccountName(account.ManagerDn),
            Enabled = account.Enabled
        };
        summary.Groups.AddRange(_directory.GetGroups(account.SamAccountName));
        return summary;
    }

    private ProvisioningResult Run(ProvisioningCommand command, string caller, string? sam, Func<ProvisioningResult, string> action)
    {
        if (string.IsNullOrWhiteSpace(command.RequestNumber) || string.IsNullOrWhiteSpace(command.Step))
            return ProvisioningResult.Fail("RequestNumber and Step are required.", false);

        var done = _journal.FindCompleted(command.RequestNumber, command.Step);
        if (done != null)
        {
            done.AlreadyDone = true;
            return done;
        }

        var result = new ProvisioningResult { DomainController = _directory.DomainController };
        try
        {
            sam = action(result);
            result.Succeeded = true;
            _logger.LogInformation("{Step} for {Request} done for {Sam} by {Caller}", command.Step, command.RequestNumber, sam, caller);
        }
        catch (Exception ex) when (TransientErrors.IsTransient(ex))
        {
            _logger.LogWarning(ex, "{Step} for {Request} failed temporarily", command.Step, command.RequestNumber);
            result.Succeeded = false;
            result.Retryable = true;
            result.Messages.Add(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Step} for {Request} failed", command.Step, command.RequestNumber);
            result.Succeeded = false;
            result.Retryable = false;
            result.Messages.Add(ex.Message);
        }

        _journal.Record(command.RequestNumber, command.Step, sam ?? result.SamAccountName, caller, result);
        return result;
    }

    private DirectoryAccount Require(string samAccountName) =>
        _directory.FindUser(samAccountName) ?? throw new KeyNotFoundException($"Account {samAccountName} does not exist.");

    private void AddGroups(string sam, IEnumerable<string> groups, ProvisioningResult result)
    {
        foreach (var group in groups)
        {
            if (!_directory.AddToGroup(sam, group))
                result.Messages.Add($"Group {group} does not exist and was skipped.");
        }
    }

    private static void Fill(ProvisioningResult result, DirectoryAccount account)
    {
        result.SamAccountName = account.SamAccountName;
        result.UserPrincipalName = account.UserPrincipalName;
        result.Mail = account.Mail;
        result.DistinguishedName = account.DistinguishedName;
        result.HomeDirectory ??= account.HomeDirectory;
    }
}
