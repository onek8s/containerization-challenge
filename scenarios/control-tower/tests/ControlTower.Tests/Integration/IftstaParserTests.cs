using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Edifact;
using Xunit;

namespace Proseware.ControlTower.Tests.Integration;

public class IftstaParserTests
{
    [Fact]
    public void Maps_road_partner_statuses_to_milestones()
    {
        var interchange = EdifactReader.Parse(Samples.Edifact("IFTSTA_003712345678_NRL2610291532.edi"));
        var statuses = new IftstaParser().Read(interchange.Messages[0]);

        Assert.Equal(4, statuses.Count);
        var pickedUp = statuses[0];
        Assert.Equal("PF26-0000039", pickedUp.ShipmentNumber);
        Assert.Equal("NRDL0000030", pickedUp.CarrierReference);
        Assert.Equal(MilestoneCodes.PickedUp, pickedUp.MilestoneCode);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 8, 12, 0, TimeSpan.Zero), pickedUp.OccurredAt);
        Assert.Equal("FITRE-NWF", pickedUp.LocationCode);
        Assert.Equal("364300123450003008", pickedUp.Sscc);
        Assert.Equal(MilestoneCodes.ArrivedAtTerminal, statuses[1].MilestoneCode);
        Assert.Equal("PF26-0000104", statuses[2].ShipmentNumber);
        Assert.Equal(MilestoneCodes.Arrived, statuses[2].MilestoneCode);
        Assert.Null(statuses[3].MilestoneCode);
        Assert.Equal("99", statuses[3].CarrierStatusCode);
    }

    [Fact]
    public void Ocean_partner_uses_header_reference_and_zone_times()
    {
        var interchange = EdifactReader.Parse(Samples.Edifact("IFTSTA_OCNXEDI_OC88213.edi"));
        var statuses = new IftstaParser().Read(interchange.Messages[0]);

        Assert.Equal(new[] { MilestoneCodes.Loaded, MilestoneCodes.Departed }, statuses.Select(s => s.MilestoneCode));
        Assert.All(statuses, s => Assert.Equal("OCNX2400031", s.CarrierReference));
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 17, 30, 0, TimeSpan.Zero), statuses[0].OccurredAt!.Value.ToUniversalTime());
        Assert.Equal("Helsinki Vuosaari", statuses[0].LocationText);
    }

    [Fact]
    public void Partner_overrides_extend_the_default_map()
    {
        var interchange = EdifactReader.Parse(Samples.Edifact("IFTSTA_003712345678_NRL2610291532.edi"));
        var statuses = new IftstaParser(new Dictionary<string, string> { ["99"] = MilestoneCodes.DeliveryAttempted }).Read(interchange.Messages[0]);
        Assert.Equal(MilestoneCodes.DeliveryAttempted, statuses[3].MilestoneCode);
    }

    [Fact]
    public void Unmapped_status_becomes_a_remark_on_the_event()
    {
        var status = new IftstaStatus("PF26-0000104", null, null, "99", null, null, null, null, "Driver waiting");
        var e = IftstaParser.ToEvent(status, "003712345678", new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(MilestoneCodes.EtaChanged, e.MilestoneCode);
        Assert.StartsWith("Unmapped carrier status 99", e.Remarks);
        Assert.Equal("edi:003712345678", e.Source);
        Assert.Equal("PF26-0000104", e.ShipmentNumber);
    }
}
