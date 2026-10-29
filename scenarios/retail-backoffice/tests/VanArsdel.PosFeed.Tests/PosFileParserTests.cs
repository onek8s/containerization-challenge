using System.Text;
using VanArsdel.PosFeed.Parsing;
using Xunit;

namespace VanArsdel.PosFeed.Tests;

public class PosFileParserTests
{
    private const string Header = "H;FI012;20261029;001;EUR\n";

    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Samples", "POS_FI012_20261029_001.txt");

    [Fact]
    public void Sample_file_is_read_with_finnish_decimals()
    {
        using var stream = File.OpenRead(SamplePath);

        var batch = PosFileParser.Parse(SamplePath, stream);

        Assert.Equal("FI012", batch.StoreCode);
        Assert.Equal(new DateTime(2026, 10, 29), batch.BusinessDate);
        Assert.Equal("EUR", batch.Currency);
        Assert.Equal(3, batch.Receipts.Count);
        Assert.Equal("POS_FI012_20261029_001.txt", batch.FileName);

        var discounted = batch.Receipts[1];
        Assert.Equal(79.80m, discounted.Total);
        Assert.Equal("AUTUMN20", discounted.Lines[0].PromotionCode);
        Assert.Equal(2, discounted.Payments.Count);

        Assert.True(batch.Receipts[2].IsReturn);
        Assert.Equal(-1, batch.Receipts[2].Lines[0].Quantity);
        Assert.Equal("643800000017", batch.Receipts[0].LoyaltyCardNo);
        Assert.Null(batch.Receipts[1].LoyaltyCardNo);
    }

    [Fact]
    public void Same_content_gives_the_same_batch_id()
    {
        var text = File.ReadAllText(SamplePath, PosFileParser.FileEncoding);

        var first = PosFileParser.Parse("POS_FI012_20261029_001.txt", text);
        var again = PosFileParser.Parse(@"\\router\pos\pos_fi012_20261029_001.txt".Replace('\\', '/'), text);
        var changed = PosFileParser.Parse("POS_FI012_20261029_001.txt", text.Replace("10:15:22", "10:15:23"));

        Assert.Equal(first.BatchId, again.BatchId);
        Assert.NotEqual(first.BatchId, changed.BatchId);
    }

    [Fact]
    public void Latin1_characters_in_sku_text_survive()
    {
        var bytes = PosFileParser.FileEncoding.GetBytes(Header + "R;1;T1;2026-10-29 10:00:00;C1;;0;10,00;2,00\nL;VA-ÅÄÖ-1;1;10,00;0,00;10,00;\nP;CASH;10,00\nT;1;10,00\n");

        var batch = PosFileParser.Parse("POS_FI012_20261029_002.txt", new MemoryStream(bytes));

        Assert.Equal("VA-ÅÄÖ-1", batch.Receipts[0].Lines[0].Sku);
    }

    [Theory]
    [InlineData("R;1;T1;2026-10-29 10:00:00;C1;;0;10,00;2,00\nP;CASH;10,00\n", "Trailer record missing")]
    [InlineData("R;1;T1;2026-10-29 10:00:00;C1;;0;10,00;2,00\nP;CASH;10,00\nT;2;10,00\n", "Trailer says 2 receipts")]
    [InlineData("R;1;T1;2026-10-29 10:00:00;C1;;0;10,00;2,00\nP;CASH;10,00\nT;1;12,00\n", "does not match receipts")]
    [InlineData("R;1;T1;2026-10-29 10:00:00;C1;;0;10,00;2,00\nP;CASH;9,00\nT;1;10,00\n", "payments 9")]
    [InlineData("L;A;1;10,00;0,00;10,00;\n", "Line without receipt")]
    [InlineData("X;1\n", "Unknown record type 'X'")]
    [InlineData("R;1;T1;29.10.2026 10:00;C1;;0;10,00;2,00\n", "Line 2")]
    public void Broken_files_are_rejected_with_the_reason(string body, string expected)
    {
        var ex = Assert.Throws<PosFileException>(() => PosFileParser.Parse("POS_FI012_20261029_003.txt", Header + body));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Header_is_required()
    {
        var ex = Assert.Throws<PosFileException>(() => PosFileParser.Parse("x.txt", "R;1;T1;2026-10-29 10:00:00;C1;;0;10,00;2,00\n"));

        Assert.Contains("Receipt before header", ex.Message);
    }

    [Fact]
    public void Windows_line_endings_and_comments_are_accepted()
    {
        var text = "# TLOG export v3\r\n" + Header.Replace("\n", "\r\n") + "T;0;0,00\r\n";

        var batch = PosFileParser.Parse("POS_FI012_20261029_004.txt", text);

        Assert.Empty(batch.Receipts);
        Assert.NotEqual(Guid.Empty, batch.BatchId);
    }

    [Fact]
    public void Encoding_is_latin1()
    {
        Assert.Equal(Encoding.Latin1.WebName, PosFileParser.FileEncoding.WebName);
    }
}
