using BatchRelease.Core.Model;

namespace BatchRelease.Core.Rules;

/// <summary>Fixed meanings shown to and confirmed by the signer (21 CFR 11.50).</summary>
public static class SignatureMeanings
{
    public const string Review =
        "I have reviewed the executed batch record, laboratory results and deviations of this batch and found them complete and correct.";

    public const string Release =
        "As Qualified Person I certify that this batch has been manufactured and checked in compliance with the marketing authorisation and GMP, and I release it for sale.";

    public const string Reject =
        "As Qualified Person I reject this batch. It must not be released for sale.";

    public const string Hold =
        "I place this batch on hold pending further investigation.";

    public const string Resume =
        "I have assessed the reason for the hold and return this batch to the release process.";

    public static string For(DecisionKind kind) => kind switch
    {
        DecisionKind.Review => Review,
        DecisionKind.Release => Release,
        DecisionKind.Reject => Reject,
        DecisionKind.Hold => Hold,
        DecisionKind.Resume => Resume,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static bool RequiresComment(DecisionKind kind) => kind is DecisionKind.Reject or DecisionKind.Hold or DecisionKind.Resume;

    public static bool RequiresSigningKey(DecisionKind kind) => kind is DecisionKind.Release or DecisionKind.Reject;
}
