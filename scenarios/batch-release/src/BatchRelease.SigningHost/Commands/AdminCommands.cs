using System.Security.Principal;
using BatchRelease.Core.Model;
using BatchRelease.Core.Security;
using BatchRelease.Data;
using BatchRelease.SigningHost.Signing;
using BatchRelease.Vault;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BatchRelease.SigningHost.Commands;

/// <summary>
/// Administrative commands, run elevated on the signing host:
/// <c>BatchRelease.SigningHost.exe enroll --account CORP\avirtanen --name "Anna Virtanen"</c>
/// </summary>
public static class AdminCommands
{
    public static readonly string[] Names = { "enroll", "revoke", "list-signers", "check-access", "install-audit-source", "uninstall-audit-source", "help" };

    public static bool IsCommand(string[] args) => args.Length > 0 && Names.Contains(args[0], StringComparer.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string[] args)
    {
        // Settings are read from the install folder, wherever the command is started from.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal) || a.Contains('=')).ToArray(),
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Services.Configure<SigningOptions>(builder.Configuration.GetSection(SigningOptions.Section));
        builder.Services.Configure<VaultOptions>(builder.Configuration.GetSection(VaultOptions.Section));
        builder.Services.AddBatchReleaseData(builder.Configuration);
        builder.Services.AddSingleton<SigningKeyStore>();
        builder.Services.AddSingleton<SignerCertificates>();
        builder.Services.AddSingleton<AccessEvaluator>();
        using var host = builder.Build();

        var options = Options(args);
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "enroll":
                    return await EnrollAsync(host.Services, Required(options, "account"), Required(options, "name"), options.ContainsKey("replace"));
                case "revoke":
                    return await RevokeAsync(host.Services, Required(options, "account"));
                case "list-signers":
                    return await ListAsync(host.Services);
                case "check-access":
                    return await CheckAccessAsync(host.Services, Required(options, "account"), Required(options, "family"));
                case "install-audit-source":
                    {
                        var vault = host.Services.GetRequiredService<IOptions<VaultOptions>>().Value;
                        SecurityAudit.Install(vault.AuditSourceName, Environment.ProcessPath!);
                        Console.WriteLine($"Security event source '{vault.AuditSourceName}' installed.");
                        return 0;
                    }
                case "uninstall-audit-source":
                    {
                        var vault = host.Services.GetRequiredService<IOptions<VaultOptions>>().Value;
                        SecurityAudit.Uninstall(vault.AuditSourceName);
                        Console.WriteLine($"Security event source '{vault.AuditSourceName}' removed.");
                        return 0;
                    }
                default:
                    Usage();
                    return 0;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IdentityNotMappedException or System.Security.Cryptography.CryptographicException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> EnrollAsync(IServiceProvider services, string account, string displayName, bool replace)
    {
        var keys = services.GetRequiredService<SigningKeyStore>();
        var certificates = services.GetRequiredService<SignerCertificates>();
        var signing = services.GetRequiredService<IOptions<SigningOptions>>().Value;
        var db = services.GetRequiredService<BatchReleaseContext>();

        var ntAccount = new NTAccount(account);
        var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
        var canonical = ((NTAccount)sid.Translate(typeof(NTAccount))).Value;

        Console.WriteLine($"Creating signing key for {canonical} ({sid}) in '{signing.KeyStorageProvider}'...");
        var key = keys.CreateKey(sid, replace);
        if (signing.RequireHardwareKeys && !key.Hardware)
        {
            keys.DeleteKey(sid);
            throw new InvalidOperationException($"'{key.Provider}' did not create a hardware key. Check the TPM (Get-Tpm) or set Signing:RequireHardwareKeys.");
        }

        var upn = account.Contains('@') ? account : $"{canonical.Split('\\')[^1]}@{canonical.Split('\\')[0]}";
        using var cert = certificates.Issue(displayName, upn, key.PublicKey);

        foreach (var existing in await db.Signers.Where(s => s.Sid == sid.Value && s.Active).ToListAsync())
            existing.Active = false;

        db.Signers.Add(new Signer
        {
            AccountName = canonical,
            Sid = sid.Value,
            DisplayName = displayName,
            KeyName = key.KeyName,
            KeyProvider = key.Provider,
            HardwareKey = key.Hardware,
            Thumbprint = cert.Thumbprint,
            CertificateDer = cert.RawData,
            ValidFromUtc = cert.NotBefore.ToUniversalTime(),
            ValidToUtc = cert.NotAfter.ToUniversalTime(),
            EnrolledUtc = DateTime.UtcNow,
            EnrolledBy = WindowsIdentity.GetCurrent().Name,
            Active = true,
        });
        await db.SaveChangesAsync();

        Console.WriteLine($"Enrolled {displayName}: key {key.KeyName} ({(key.Hardware ? "hardware" : "software")}), certificate {cert.Thumbprint} valid until {cert.NotAfter:yyyy-MM-dd}.");
        return 0;
    }

    private static async Task<int> RevokeAsync(IServiceProvider services, string account)
    {
        var keys = services.GetRequiredService<SigningKeyStore>();
        var db = services.GetRequiredService<BatchReleaseContext>();
        var sid = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));

        var signers = await db.Signers.Where(s => s.Sid == sid.Value && s.Active).ToListAsync();
        foreach (var signer in signers)
            signer.Active = false;
        await db.SaveChangesAsync();
        keys.DeleteKey(sid);

        Console.WriteLine($"Revoked {signers.Count} enrolment(s) of {account} and deleted the key.");
        return 0;
    }

    private static async Task<int> ListAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<BatchReleaseContext>();
        foreach (var s in await db.Signers.AsNoTracking().OrderBy(s => s.DisplayName).ThenByDescending(s => s.EnrolledUtc).ToListAsync())
        {
            Console.WriteLine($"{(s.Active ? "active " : "revoked")}  {s.DisplayName,-28} {s.AccountName,-28} {(s.HardwareKey ? "hw" : "sw")}  {s.Thumbprint}  until {s.ValidToUtc:yyyy-MM-dd}");
        }
        return 0;
    }

    private static async Task<int> CheckAccessAsync(IServiceProvider services, string account, string familyCode)
    {
        var db = services.GetRequiredService<BatchReleaseContext>();
        var access = services.GetRequiredService<AccessEvaluator>();
        var family = await db.ProductFamilies.AsNoTracking().FirstOrDefaultAsync(f => f.Code == familyCode)
            ?? throw new ArgumentException($"Product family {familyCode} does not exist.");
        var sid = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        var rights = access.GrantedRights(sid, family.PolicySddl);
        Console.WriteLine($"{account} on {family.Name}: {(rights == ReleaseRights.None ? "no access" : string.Join(", ", rights.Names()))}");
        return 0;
    }

    private static Dictionary<string, string> Options(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || args[i].Contains('='))
                continue;
            var name = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
            result[name] = value;
        }
        return result;
    }

    private static string Required(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) && value != "true"
            ? value
            : throw new ArgumentException($"--{name} is required.");

    private static void Usage()
    {
        Console.WriteLine("""
            BatchRelease.SigningHost.exe                     run the signing host
            BatchRelease.SigningHost.exe enroll --account DOMAIN\user --name "Display Name" [--replace]
            BatchRelease.SigningHost.exe revoke --account DOMAIN\user
            BatchRelease.SigningHost.exe list-signers
            BatchRelease.SigningHost.exe check-access --account DOMAIN\user --family CODE
            BatchRelease.SigningHost.exe install-audit-source | uninstall-audit-source
            """);
    }
}
