using Microsoft.Extensions.Options;

namespace Litware.DocControl.Web.Services;

public sealed class CurrentUser
{
    private readonly IHttpContextAccessor _http;
    private readonly PortalOptions _options;

    public CurrentUser(IHttpContextAccessor http, IOptions<PortalOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <summary>DOMAIN\account as delivered by Windows authentication.</summary>
    public string Name => _http.HttpContext?.User.Identity?.Name ?? "unknown";

    public string AccountName => Name.Contains('\\') ? Name[(Name.IndexOf('\\') + 1)..] : Name;

    public string MailAddress => string.Format(System.Globalization.CultureInfo.InvariantCulture, _options.MailAddressFormat, AccountName.ToLowerInvariant());

    public bool IsController => _http.HttpContext?.User.IsInRole(_options.ControllersGroup) == true;
}
