namespace Proseware.ControlTower.Integration.Edifact;

/// <summary>Syntax and service report (CONTRL D.3) acknowledging a received interchange.</summary>
public static class ContrlBuilder
{
    /// <summary>UCI action codes: 7 = acknowledged, 4 = rejected, 8 = received.</summary>
    public static EdifactMessage Acknowledge(EdifactInterchange received, string messageReference, IReadOnlyCollection<(EdifactMessage Message, string? Error)>? results = null)
    {
        var m = new EdifactMessage { ReferenceNumber = messageReference, Type = "CONTRL", Version = "D", Release = "3", Agency = "UN" };
        var anyError = results?.Any(r => r.Error is not null) ?? false;
        var allError = results is { Count: > 0 } && results.All(r => r.Error is not null);
        m.Segments.Add(EdifactSegment.Create("UCI",
            received.ControlReference,
            new[] { received.SenderId, received.SenderQualifier ?? "" },
            new[] { received.RecipientId, received.RecipientQualifier ?? "" },
            allError ? "4" : "7"));
        if (anyError && results is not null)
        {
            foreach (var (message, error) in results)
            {
                m.Segments.Add(EdifactSegment.Create("UCM",
                    message.ReferenceNumber,
                    new[] { message.Type, message.Version, message.Release, message.Agency },
                    error is null ? "7" : "4",
                    error is null ? null : "13"));
            }
        }
        return m;
    }

    public static (string ControlReference, bool Accepted) Read(EdifactMessage contrl)
    {
        var uci = contrl.First("UCI") ?? throw new EdifactException("CONTRL has no UCI segment.");
        return (uci.Get(0), uci.Get(3) is "7" or "8");
    }
}
