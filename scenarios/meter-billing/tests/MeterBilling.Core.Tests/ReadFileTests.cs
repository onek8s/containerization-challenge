using System.Text;
using Coho.Billing.Core.Reads;
using Xunit;

namespace Coho.Billing.Core.Tests;

public class ReadFileTests
{
    [Fact]
    public void Handheld_sample_is_parsed_and_not_read_meters_are_listed()
    {
        using var stream = File.OpenRead(TestData.Sample("N1_R12_20260428.csv"));
        var file = ReadFileParsers.Parse("N1_R12_20260428.csv", stream);

        Assert.Equal(ReadSource.Handheld, file.Source);
        Assert.Equal(4, file.Reads.Count);
        Assert.Single(file.RejectedLines);
        Assert.Contains("W21B110230", file.RejectedLines[0]);

        var first = file.Reads[0];
        Assert.Equal("W19A004417", first.MeterSerial);
        Assert.Equal(new DateTime(2026, 4, 28), first.ReadDate);
        Assert.Equal(1863.1m, first.Reading);
        Assert.Equal("N1-R12", first.Route);
        Assert.Equal("HH07-JPA", first.ReaderId);
        Assert.Equal(9.8m, file.Reads[2].Reading);
    }

    [Fact]
    public void Handheld_file_with_wrong_header_is_rejected()
    {
        var parser = new HandheldReadParser();
        var ex = Assert.Throws<ReadFileException>(() => parser.Parse("x.csv", new[] { "ROUTE;METER;DATE", "A;B;C" }));
        Assert.Equal(1, ex.LineNumber);
    }

    [Fact]
    public void Handheld_bad_lines_are_skipped()
    {
        var lines = new[]
        {
            HandheldReadParser.Header,
            "R1;1;M1;31.04.2026;08:00;10,0;00;H1",
            "R1;2;M2;28.04.2026;08:00;abc;00;H1",
            "R1;3;M3;28.04.2026;08:00;5;00",
            "R1;4;m4;28.04.2026;08:00;12;;H1"
        };

        var file = new HandheldReadParser().Parse("x.csv", lines);

        Assert.Single(file.Reads);
        Assert.Equal("M4", file.Reads[0].MeterSerial);
        Assert.Equal("00", file.Reads[0].ReaderCode);
        Assert.Equal(3, file.RejectedLines.Count);
    }

    [Fact]
    public void Amr_sample_is_parsed_with_implied_decimal()
    {
        using var stream = File.OpenRead(TestData.Sample("AMR0004471.dat"));
        var file = ReadFileParsers.Parse("AMR0004471.dat", stream);

        Assert.Equal(ReadSource.Amr, file.Source);
        Assert.Equal("AMR0004471", file.BatchId);
        Assert.Equal(3, file.Reads.Count);
        Assert.Equal(49350.2m, file.Reads[0].Reading);
        Assert.Equal(new DateTime(2026, 4, 30), file.Reads[0].ReadDate);
        Assert.Single(file.RejectedLines);
        Assert.Contains("no response", file.RejectedLines[0]);
    }

    [Fact]
    public void Amr_trailer_count_must_match()
    {
        var lines = new[]
        {
            "HCOHOAMR20260430061512AMR0000001",
            "DW20K700045  20260430000049350200071",
            "T00000002"
        };

        var ex = Assert.Throws<ReadFileException>(() => new AmrReadParser().Parse("a.dat", lines));
        Assert.Contains("does not match", ex.Message);
    }

    [Fact]
    public void Amr_without_trailer_is_incomplete()
    {
        var lines = new[] { "HCOHOAMR20260430061512AMR0000001", "DW20K700045  20260430000049350200071" };
        Assert.Throws<ReadFileException>(() => new AmrReadParser().Parse("a.dat", lines));
    }

    [Fact]
    public void Amr_leak_and_tamper_flags_are_mapped()
    {
        var lines = new[]
        {
            "HCOHOAMR20260430061512AMR0000001",
            "DW20K700045  20260430000049350203071",
            "DW20K700046  20260430000049350201071",
            "T00000002"
        };

        var file = new AmrReadParser().Parse("a.dat", lines);

        Assert.Equal("LK", file.Reads[0].ReaderCode);
        Assert.Equal("A01", file.Reads[1].ReaderCode);
    }

    [Fact]
    public void Unknown_format_is_rejected()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("hello\r\nworld\r\n"));
        Assert.Throws<ReadFileException>(() => ReadFileParsers.Parse("x.txt", stream));
    }
}
