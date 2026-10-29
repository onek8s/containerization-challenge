using System.Text;
using ContosoFoods.TradeRebates.Rules;

namespace TradeRebates.Tests;

public class ClaimFileTests
{
    static ClaimFileTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const string Header = "HHEINON  20260920261002\r\n";

    private static string Detail(string invoice, int qty, long cents) =>
        "D" + invoice.PadRight(12) + "20260911" + "88001".PadRight(10) + "06411401015090" + qty.ToString().PadLeft(9) + cents.ToString().PadLeft(12) + "\r\n";

    private static string Trailer(int count, long cents) => "T" + count.ToString().PadLeft(7, '0') + cents.ToString().PadLeft(14) + "\r\n";

    private static ClaimFile ParseSample(string name, int codePage)
    {
        using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "samples", name), Encoding.GetEncoding(codePage));
        return ClaimFileParser.Parse(reader);
    }

    [Fact]
    public void Csv_sample_accepts_valid_lines_and_reports_errors()
    {
        var file = ParseSample("kespro-202609.csv", 1252);
        Assert.Equal("CSV", file.Format);
        Assert.Equal("KESPRO", file.DistributorCode);
        Assert.Equal(202609, file.Period);
        Assert.Equal(3, file.Lines.Count);
        Assert.Equal(2, file.Errors.Count);
        Assert.Contains(file.Errors, e => e.LineNumber == 4 && e.Message.Contains("date"));
        Assert.Contains(file.Errors, e => e.LineNumber == 5 && e.Message.Contains("check digit"));
        Assert.Equal(288.50m, file.Lines[1].NetAmount);
        Assert.Equal(new DateTime(2026, 9, 4), file.Lines[1].InvoiceDate);
        Assert.Equal("00000096385074", file.Lines[1].Gtin);
        Assert.Equal(139m, file.TotalQuantity);
        Assert.Equal(1668.50m, file.TotalAmount);
    }

    [Fact]
    public void Fixed_width_sample_is_read_with_trailer_check()
    {
        var file = ParseSample("heinon-202609.so", 850);
        Assert.Equal("SO", file.Format);
        Assert.Equal("HEINON", file.DistributorCode);
        Assert.Equal(202609, file.Period);
        Assert.Empty(file.Errors);
        Assert.Equal(3, file.Lines.Count);
        Assert.Equal(-24.00m, file.Lines[2].NetAmount);
        Assert.Equal(696.00m, file.TotalAmount);
        Assert.Equal("700124", file.Lines[1].InvoiceNo);
        Assert.Equal("88002", file.Lines[1].CustomerNo);
    }

    [Fact]
    public void Fixed_width_trailer_count_mismatch_is_reported()
    {
        string text = Header + Detail("700123", 48, 57600) + Trailer(2, 57600);
        var file = ClaimFileParser.Parse(new StringReader(text));
        Assert.Single(file.Lines);
        Assert.Contains(file.Errors, e => e.Message.Contains("Trailer says 2"));
    }

    [Fact]
    public void Fixed_width_trailer_total_mismatch_is_reported()
    {
        string text = Header + Detail("700123", 48, 57600) + Trailer(1, 57601);
        var file = ClaimFileParser.Parse(new StringReader(text));
        Assert.Contains(file.Errors, e => e.Message.Contains("total"));
    }

    [Fact]
    public void Missing_trailer_is_reported()
    {
        string text = Header + Detail("700123", 48, 57600);
        var file = ClaimFileParser.Parse(new StringReader(text));
        Assert.Contains(file.Errors, e => e.Message.Contains("Trailer record missing"));
    }

    [Fact]
    public void Csv_rejects_mixed_distributors_and_periods()
    {
        string text = ClaimFileParser.CsvHeader + "\n" +
                      "KESPRO;202609;A1;2026-09-01;C1;96385074;1;10\n" +
                      "METRO;202609;A2;2026-09-01;C1;96385074;1;10\n" +
                      "KESPRO;202610;A3;2026-10-01;C1;96385074;1;10\n";
        var file = ClaimFileParser.Parse(new StringReader(text));
        Assert.Single(file.Lines);
        Assert.Equal(2, file.Errors.Count);
    }

    [Fact]
    public void Csv_with_bom_and_blank_lines()
    {
        string text = "\n﻿" + ClaimFileParser.CsvHeader + "\n\nKESPRO;202609;A1;20260901;C1;96385074;2;20.5\n";
        var file = ClaimFileParser.Parse(new StringReader(text));
        Assert.Empty(file.Errors);
        Assert.Single(file.Lines);
    }

    [Theory]
    [InlineData("", "EMPTY")]
    [InlineData("hello;world\n", "UNKNOWN")]
    public void Unknown_or_empty_files(string text, string format)
    {
        var file = ClaimFileParser.Parse(new StringReader(text));
        Assert.Equal(format, file.Format);
        Assert.Single(file.Errors);
    }

    [Theory]
    [InlineData(202601, true)]
    [InlineData(202612, true)]
    [InlineData(202613, false)]
    [InlineData(199912, false)]
    [InlineData(20260, false)]
    public void Period_validation(int period, bool valid)
    {
        Assert.Equal(valid, ClaimFileParser.IsValidPeriod(period));
    }
}
