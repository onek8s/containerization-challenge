using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Caching.Memory;

namespace Wingtip.Invoices.Admin.Services;

/// <summary>
/// Display names for DOMAIN\account names shown in history and release columns.
/// </summary>
public class UserDirectory
{
    private readonly IMemoryCache cache;
    private readonly ILogger<UserDirectory> logger;
    private readonly string domain;
    private readonly TimeSpan lifetime;

    public UserDirectory(IMemoryCache cache, IConfiguration configuration, ILogger<UserDirectory> logger)
    {
        this.cache = cache;
        this.logger = logger;
        domain = configuration["Directory:Domain"];
        lifetime = TimeSpan.FromMinutes(configuration.GetValue("Directory:CacheMinutes", 60));
    }

    public string DisplayName(string account)
    {
        if (string.IsNullOrEmpty(account) || !account.Contains('\\')) return account;

        return cache.GetOrCreate("dn:" + account.ToLowerInvariant(), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = lifetime;
            try
            {
                using var context = new PrincipalContext(ContextType.Domain, domain);
                using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, account);
                if (user != null && !string.IsNullOrEmpty(user.DisplayName)) return user.DisplayName;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Directory lookup for {Account} failed", account);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            }
            return account;
        });
    }
}
