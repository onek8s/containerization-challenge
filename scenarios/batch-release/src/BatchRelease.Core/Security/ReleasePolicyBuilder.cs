using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BatchRelease.Core.Security;

/// <summary>
/// Builds the SDDL release policy for a product family. Each entry grants a set of
/// <see cref="ReleaseRights"/> to a security principal (user or group SID).
/// </summary>
public sealed partial class ReleasePolicyBuilder
{
    private readonly List<(bool Deny, ReleaseRights Rights, string Sid)> _entries = new();
    private string _owner = "BA";
    private string _group = "SY";

    public ReleasePolicyBuilder Owner(string sid)
    {
        _owner = CheckSid(sid);
        return this;
    }

    public ReleasePolicyBuilder Group(string sid)
    {
        _group = CheckSid(sid);
        return this;
    }

    public ReleasePolicyBuilder Allow(string sid, ReleaseRights rights)
    {
        _entries.Add((false, rights, CheckSid(sid)));
        return this;
    }

    public ReleasePolicyBuilder Deny(string sid, ReleaseRights rights)
    {
        _entries.Add((true, rights, CheckSid(sid)));
        return this;
    }

    /// <summary>
    /// Returns the SDDL string. Deny entries are written first (canonical ACL order) and the DACL
    /// is protected so nothing is inherited.
    /// </summary>
    public string Build()
    {
        if (_entries.Count == 0)
            throw new InvalidOperationException("A release policy needs at least one entry.");

        var sb = new StringBuilder();
        sb.Append("O:").Append(_owner).Append("G:").Append(_group).Append("D:P");
        foreach (var e in _entries.Where(e => e.Deny))
            Append(sb, "D", e.Rights, e.Sid);
        foreach (var e in _entries.Where(e => !e.Deny))
            Append(sb, "A", e.Rights, e.Sid);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string type, ReleaseRights rights, string sid) =>
        sb.Append('(').Append(type).Append(";;0x").Append(((uint)rights).ToString("x", CultureInfo.InvariantCulture))
          .Append(";;;").Append(sid).Append(')');

    private static string CheckSid(string sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
            throw new ArgumentException("SID is required.", nameof(sid));
        sid = sid.Trim();
        if (!SidPattern().IsMatch(sid) && !WellKnownAlias().IsMatch(sid))
            throw new ArgumentException($"'{sid}' is not a SID.", nameof(sid));
        return sid;
    }

    [GeneratedRegex(@"^S-1-\d+(-\d+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex SidPattern();

    [GeneratedRegex(@"^(BA|SY|AU|BU|NS|LS)$", RegexOptions.CultureInvariant)]
    private static partial Regex WellKnownAlias();
}
