using Proseware.ControlTower.Integration.Edifact;
using Xunit;

namespace Proseware.ControlTower.Tests.Integration;

public class EdifactWriterTests
{
    [Fact]
    public void Round_trip_keeps_segments_and_escapes()
    {
        var original = EdifactReader.Parse(Samples.Edifact("IFTSTA_003712345678_NRL2610291532.edi"));
        var written = EdifactWriter.Write(original);
        var reparsed = EdifactReader.Parse(written);

        Assert.Equal(original.ControlReference, reparsed.ControlReference);
        Assert.Equal(original.Messages[0].Segments.Count, reparsed.Messages[0].Segments.Count);
        for (var i = 0; i < original.Messages[0].Segments.Count; i++)
            Assert.Equal(original.Messages[0].Segments[i].ToString(), reparsed.Messages[0].Segments[i].ToString());
        Assert.Contains("ramp?:", written);
    }

    [Fact]
    public void Trailing_empty_elements_are_dropped()
    {
        var interchange = new EdifactInterchange { SenderId = "A", RecipientId = "B", Prepared = new DateTime(2026, 1, 2, 3, 4, 0), ControlReference = "1" };
        var m = new EdifactMessage { ReferenceNumber = "1", Type = "IFTSTA" };
        m.Segments.Add(EdifactSegment.Create("FTX", "AAA", null, null, new[] { "Text", "", "" }, null));
        interchange.Messages.Add(m);

        var text = EdifactWriter.Write(interchange, writeUna: false, lineBreaks: false);

        Assert.Equal("UNB+UNOC:3+A+B+260102:0304+1'UNH+1+IFTSTA:D:96A:UN'FTX+AAA+++Text'UNT+3+1'UNZ+1+1'", text);
    }

    [Fact]
    public void Contrl_acknowledges_and_reports_failed_messages()
    {
        var received = EdifactReader.Parse(Samples.Edifact("IFTSTA_003712345678_NRL2610291532.edi"));
        var ok = ContrlBuilder.Acknowledge(received, "9");
        Assert.Equal(("NRL2610291532", true), ContrlBuilder.Read(ok));

        var failed = ContrlBuilder.Acknowledge(received, "9", new[] { (received.Messages[0], (string?)"Unknown shipment") });
        Assert.Equal(("NRL2610291532", false), ContrlBuilder.Read(failed));
        Assert.Equal("4", failed.First("UCM")!.Get(2));
    }
}
