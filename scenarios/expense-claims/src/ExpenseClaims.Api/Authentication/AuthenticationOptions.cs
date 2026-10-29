namespace ExpenseClaims.Api.Authentication;

public sealed class ExpensesAuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary><c>Adfs</c> (default) or <c>Development</c>.</summary>
    public string Mode { get; set; } = "Adfs";

    public AdfsOptions Adfs { get; set; } = new();
    public DevelopmentAuthOptions Development { get; set; } = new();

    public bool IsDevelopmentMode => string.Equals(Mode, "Development", StringComparison.OrdinalIgnoreCase);
}

public sealed class AdfsOptions
{
    /// <summary>OpenID Connect authority, for example https://adfs.corp.local/adfs.</summary>
    public string Authority { get; set; } = "https://adfs.corp.local/adfs";

    /// <summary>Relying party identifier of the Web API in the ADFS application group.</summary>
    public string Audience { get; set; } = "https://expenses-api.corp.local";

    /// <summary>
    /// Issuers accepted in access tokens. ADFS issues access tokens with
    /// http://&lt;farm&gt;/adfs/services/trust even though the discovery document names https://&lt;farm&gt;/adfs.
    /// </summary>
    public string[] ValidIssuers { get; set; } =
    {
        "http://adfs.corp.local/adfs/services/trust",
        "https://adfs.corp.local/adfs"
    };

    public bool RequireHttpsMetadata { get; set; } = true;
}

public sealed class DevelopmentAuthOptions
{
    public string Issuer { get; set; } = "expenses-dev";
    public string Audience { get; set; } = "https://expenses-api.corp.local";
    public string SigningKey { get; set; } = "";
    public int TokenLifetimeMinutes { get; set; } = 480;

    /// <summary>Roles given to test users, by UPN.</summary>
    public Dictionary<string, string[]> Roles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
