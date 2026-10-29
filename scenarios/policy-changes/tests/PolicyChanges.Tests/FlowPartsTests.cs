using System.Buffers.Binary;
using System.IO.Compression;
using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Imaging;
using PolicyChanges.Core.Requests;

namespace PolicyChanges.Tests;

public class KeySequenceTests
{
    [Fact]
    public void Parses_text_named_keys_and_modifiers()
    {
        var keys = KeySequence.Parse("ab{TAB}^+c{F8}{+}");
        Assert.Equal(6, keys.Count);
        Assert.Equal('a', keys[0].Character);
        Assert.Equal((ushort)0x09, keys[2].VirtualKey);
        Assert.Equal(new KeyStroke('C', null, KeyModifiers.Control | KeyModifiers.Shift), keys[3]);
        Assert.Equal((ushort)0x77, keys[4].VirtualKey);
        Assert.Equal('+', keys[5].Character);
    }

    [Fact]
    public void Escape_round_trips()
    {
        var text = "50% {net} +tax ^";
        Assert.Equal(text, new string(KeySequence.Parse(KeySequence.Escape(text)).Select(k => k.Character!.Value).ToArray()));
    }

    [Theory]
    [InlineData("{NOPE}")]
    [InlineData("^")]
    [InlineData("{TAB")]
    public void Rejects_bad_sequences(string keys) => Assert.Throws<FormatException>(() => KeySequence.Parse(keys));
}

public class FlowTemplateTests
{
    private static FlowContext Context() => new(new FlowRequest("PC-261029-0001", "HL-2056331", ChangeType.BeneficiaryChange,
        new DateOnly(2026, 11, 1), new Dictionary<string, string>
        {
            ["Iban"] = "FI2112345600000785",
            ["Beneficiaries"] = "Anna|1990-01-02|60\nOlle|1992-05-06|40",
            ["City"] = "Tampere",
        }));

    [Fact]
    public void Expands_placeholders_and_filters()
    {
        var c = Context();
        c.Variables["Endorsement"] = "6004101";
        Assert.Equal("HL-2056331 01.11.2026 2026-11-01", FlowTemplate.Expand("{Policy} {Effective} {Effective:yyyy-MM-dd}", c));
        Assert.Equal("FI21 1234 5600 0007 85", FlowTemplate.Expand("{Field:Iban|iban}", c));
        Assert.Equal("TAMPERE / 6004101 / 2 / {x}", FlowTemplate.Expand("{Field:City|upper} / {Var:Endorsement} / {Count:Beneficiaries} / {{x}", c));
        c.Item = new[] { "Anna", "1990-01-02", "60" };
        Assert.Equal("Anna 02.01.1990 60", FlowTemplate.Expand("{Item:0} {Item:1|date} {Item:2}", c));
    }

    [Fact]
    public void Unknown_placeholder_fails() => Assert.Throws<FormatException>(() => FlowTemplate.Expand("{Nope}", Context()));

    [Theory]
    [InlineData("PolicyDesk 6.4 - DESK01 [PROD]", "PolicyDesk 6.* - *", true)]
    [InlineData("PolicyDesk - Policy HL-2056331", "PolicyDesk - Policy HL-2056331", true)]
    [InlineData("PolicyDesk - Sign in", "PolicyDesk 6.* - *", false)]
    public void Wildcards(string title, string pattern, bool match) => Assert.Equal(match, Wildcard.IsMatch(title, pattern));
}

public class FlowDefinitionTests
{
    public static IEnumerable<object[]> FlowFiles() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "flows"), "*.json").Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Every_change_type_has_one_flow()
    {
        var flows = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "flows"), "*.json")
            .Select(f => FlowDefinition.Parse(File.ReadAllText(f))).ToList();
        Assert.Equal(Enum.GetValues<ChangeType>().OrderBy(t => t), flows.Select(f => f.ChangeType).OrderBy(t => t));
    }

    [Theory]
    [MemberData(nameof(FlowFiles))]
    public void Flow_starts_on_main_window_and_reads_endorsement(string file)
    {
        var flow = FlowDefinition.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "flows", file)));
        Assert.Equal(FlowAction.Window, flow.Steps[0].Action);
        Assert.Contains(flow.Steps, s => s.Action == FlowAction.Dialog && (s.Accept ?? new()).Any(a => a.Contains("?<Endorsement>")));
        Assert.All(flow.Steps.Where(s => s.Action == FlowAction.Dialog), s => Assert.All((s.Accept ?? new()).Concat(s.Reject ?? new()),
            p => _ = new System.Text.RegularExpressions.Regex(p)));
    }
}

public class PngEncoderTests
{
    [Fact]
    public void Writes_rgb_png_with_pixel_rows()
    {
        // 2x2 BGRA: red, green / blue, white
        var bgra = new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255 };
        var png = PngEncoder.FromBgra(bgra, 2, 2);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));

        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
        Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));
        using var z = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress);
        var raw = new MemoryStream();
        z.CopyTo(raw);
        Assert.Equal(new byte[] { 0, 255, 0, 0, 0, 255, 0, 0, 0, 0, 255, 255, 255, 255 }, raw.ToArray());
        Assert.Equal("IEND", System.Text.Encoding.ASCII.GetString(png, png.Length - 8, 4));
    }
}
