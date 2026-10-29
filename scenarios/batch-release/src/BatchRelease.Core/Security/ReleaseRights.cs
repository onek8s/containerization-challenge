using BatchRelease.Core.Model;

namespace BatchRelease.Core.Security;

/// <summary>Access mask bits used in product family release policies.</summary>
[Flags]
public enum ReleaseRights : uint
{
    None = 0,
    ReadRecord = 0x0001,
    Review = 0x0002,
    Release = 0x0004,
    Reject = 0x0008,
    Hold = 0x0010,
    ManageSigners = 0x0020,
    ManagePolicy = 0x0040,

    All = ReadRecord | Review | Release | Reject | Hold | ManageSigners | ManagePolicy,
}

public static class ReleaseRightsExtensions
{
    public static ReleaseRights RequiredFor(DecisionKind kind) => kind switch
    {
        DecisionKind.Review => ReleaseRights.Review,
        DecisionKind.Release => ReleaseRights.Release,
        DecisionKind.Reject => ReleaseRights.Reject,
        DecisionKind.Hold or DecisionKind.Resume => ReleaseRights.Hold,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static IEnumerable<string> Names(this ReleaseRights rights) =>
        Enum.GetValues<ReleaseRights>()
            .Where(r => r != ReleaseRights.None && r != ReleaseRights.All && rights.HasFlag(r))
            .Select(r => r.ToString());
}
