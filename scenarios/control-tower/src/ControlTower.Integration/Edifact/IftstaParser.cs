using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Edifact;

public sealed record IftstaStatus(
    string? ShipmentNumber,
    string? CarrierReference,
    string? Sscc,
    string CarrierStatusCode,
    string? MilestoneCode,
    DateTimeOffset? OccurredAt,
    string? LocationCode,
    string? LocationText,
    string? Remarks);

/// <summary>Reads IFTSTA status reports (D.96A / D.01B) and maps carrier status codes to milestones.</summary>
public sealed class IftstaParser
{
    public const string MessageType = "IFTSTA";

    /// <summary>Default mapping of status event codes (code list 9011) used by our road and sea partners.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultStatusMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["10"] = MilestoneCodes.PickedUp,
        ["11"] = MilestoneCodes.PickupPlanned,
        ["13"] = MilestoneCodes.Departed,
        ["14"] = MilestoneCodes.Arrived,
        ["16"] = MilestoneCodes.ArrivedAtTerminal,
        ["17"] = MilestoneCodes.DepartedTerminal,
        ["20"] = MilestoneCodes.OutForDelivery,
        ["21"] = MilestoneCodes.Delivered,
        ["22"] = MilestoneCodes.DeliveryAttempted,
        ["23"] = MilestoneCodes.Damaged,
        ["24"] = MilestoneCodes.CustomsHeld,
        ["25"] = MilestoneCodes.CustomsReleased,
        ["26"] = MilestoneCodes.EtaChanged,
        ["27"] = MilestoneCodes.Loaded,
        ["28"] = MilestoneCodes.Discharged,
        // ocean partner (OCNX) uses its own alphanumeric codes
        ["AE"] = MilestoneCodes.Loaded,
        ["VD"] = MilestoneCodes.Departed,
        ["VA"] = MilestoneCodes.Arrived,
        ["UV"] = MilestoneCodes.Discharged,
        ["OA"] = MilestoneCodes.OutForDelivery,
        ["CR"] = MilestoneCodes.CustomsReleased,
        ["CH"] = MilestoneCodes.CustomsHeld,
    };

    private readonly IReadOnlyDictionary<string, string> _statusMap;

    public IftstaParser(IReadOnlyDictionary<string, string>? overrides = null)
    {
        if (overrides is null || overrides.Count == 0)
        {
            _statusMap = DefaultStatusMap;
            return;
        }
        var merged = new Dictionary<string, string>(DefaultStatusMap, StringComparer.Ordinal);
        foreach (var (k, v) in overrides) merged[k] = v;
        _statusMap = merged;
    }

    public IReadOnlyList<IftstaStatus> Read(EdifactMessage message)
    {
        if (message.Type != MessageType) throw new EdifactException($"Expected IFTSTA, got {message.Type}.");
        var result = new List<IftstaStatus>();

        // Header references that apply to all consignments
        var headerRef = message.Segments.TakeWhile(x => x.Tag != "CNI").FirstOrDefault(r => r.Tag == "RFF" && r.Get(0, 0) is "BN" or "BM")?.Get(0, 1);

        foreach (var group in message.Groups("CNI"))
        {
            var cni = group[0];
            string? number = NullIfEmpty(cni.Get(1, 0));
            string? carrierRef = headerRef;
            string? sscc = null;
            string? statusCode = null;
            DateTimeOffset? at = null;
            string? locCode = null, locText = null, remarks = null;

            void Flush()
            {
                if (statusCode is null) return;
                _statusMap.TryGetValue(statusCode, out var milestone);
                result.Add(new IftstaStatus(number, carrierRef, sscc, statusCode, milestone, at, locCode, locText, remarks));
                at = null;
                locCode = null;
                locText = null;
                remarks = null;
            }

            foreach (var seg in group.Skip(1))
            {
                switch (seg.Tag)
                {
                    case "STS":
                        Flush();
                        // STS+1+21' or STS+1+21::ZZZ' ; event code is first component of element 1
                        statusCode = seg.Get(1, 0);
                        break;
                    case "RFF":
                        var qualifier = seg.Get(0, 0);
                        if (qualifier is "CU" or "CN") number ??= NullIfEmpty(seg.Get(0, 1));
                        else if (qualifier is "BN" or "BM" or "AWB") carrierRef = NullIfEmpty(seg.Get(0, 1));
                        break;
                    case "DTM":
                        var dq = seg.Get(0, 0);
                        if (dq is "334" or "178" or "35" or "132" or "137")
                        {
                            var parsed = EdifactReader.ParseDtm(seg.Get(0, 1), seg.Get(0, 2));
                            if (dq != "137" || at is null) at = parsed ?? at;
                        }
                        break;
                    case "LOC":
                        locCode = NullIfEmpty(seg.Get(1, 0));
                        locText = NullIfEmpty(seg.Get(1, 3));
                        break;
                    case "FTX":
                        remarks = string.Join(" ", seg[3].Components.Where(c => c.Length > 0));
                        break;
                    case "GIN":
                        if (seg.Get(0) == "BJ") sscc = Sscc.Normalize(seg.Get(1, 0));
                        break;
                }
            }
            Flush();
        }
        return result;
    }

    public static StatusReported ToEvent(IftstaStatus status, string partnerId, DateTimeOffset receivedAt) => new(
        IntegrationEvent.NewId(),
        status.OccurredAt ?? receivedAt,
        status.ShipmentNumber is { } n && ShipmentNumber.IsValid(n) ? n : null,
        status.CarrierReference ?? (status.ShipmentNumber is { } m && !ShipmentNumber.IsValid(m) ? m : null),
        status.Sscc,
        status.MilestoneCode ?? MilestoneCodes.EtaChanged,
        status.LocationCode,
        status.LocationText,
        "edi:" + partnerId,
        status.MilestoneCode is null ? $"Unmapped carrier status {status.CarrierStatusCode}. {status.Remarks}".Trim() : status.Remarks);

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
