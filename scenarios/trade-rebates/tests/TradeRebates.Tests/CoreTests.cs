using System.Text;
using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.StatementWorker;
using Microsoft.Extensions.Options;

namespace TradeRebates.Tests;

public class CoreTests
{
    [Theory]
    [InlineData("202609", true, 202609)]
    [InlineData("2026-09", true, 202609)]
    [InlineData("202613", false, 0)]
    [InlineData("26-09", false, 0)]
    [InlineData(null, false, 0)]
    public void Period_parsing(string? text, bool ok, int expected)
    {
        Assert.Equal(ok, Periods.TryParse(text, out int p));
        if (ok)
            Assert.Equal(expected, p);
    }

    [Fact]
    public void Quarters()
    {
        Assert.Equal("2026Q3", Periods.QuarterOf(new DateTime(2026, 9, 30)));
        Assert.Equal("2026Q3", Periods.PreviousQuarter(new DateTime(2026, 10, 29)));
        Assert.Equal("2025Q4", Periods.PreviousQuarter(new DateTime(2026, 1, 5)));
        Assert.Equal([202607, 202608, 202609], Periods.PeriodsOf("2026Q3"));
        Assert.False(Periods.IsQuarter("2026Q5"));
        Assert.Throws<FormatException>(() => Periods.PeriodsOf("2026-3"));
    }

    [Fact]
    public void Api_keys_hash_and_prefix()
    {
        string key = ApiKeys.Generate();
        Assert.True(ApiKeys.LooksValid(key));
        Assert.Equal(32, ApiKeys.Hash(key).Length);
        Assert.Equal(ApiKeys.Hash(key), ApiKeys.Hash(" " + key + " "));
        Assert.Equal(key[..8], ApiKeys.Prefix(key));
        Assert.NotEqual(key, ApiKeys.Generate());
        Assert.False(ApiKeys.LooksValid("tr_short"));
    }

    internal static StatementDocument SampleStatement() => new()
    {
        Header = new StatementHeader
        {
            StatementId = 17, Quarter = "2026Q3", Amount = 1316.65m, Status = "Q", DistributorCode = "KESPRO",
            DistributorName = "Kespro Foodservice Oy", ErpVendorNo = "300145", Currency = "EUR", StatementFolder = "KESPRO",
            BankAccountMasked = "FI************0785"
        },
        Lines =
        [
            new StatementLine { AgreementNo = "TR-2026-001", Description = "Coffee <volume> bonus", Basis = "Q", Period = 202608, Quantity = 0, NetAmount = 0, YtdQuantity = 0, RatePercent = 0, YtdRebate = 0, AccruedAmount = 0, ToNextTier = 2000 },
            new StatementLine { AgreementNo = "TR-2026-001", Description = "Coffee <volume> bonus", Basis = "Q", Period = 202609, Quantity = 5500, NetAmount = 51850, YtdQuantity = 5500, YtdNetAmount = 51850, RatePercent = 2.5m, YtdRebate = 1296.25m, AccruedAmount = 1296.25m, ToNextTier = 4500 },
            new StatementLine { AgreementNo = "TR-2026-002", Description = "Total range", Basis = "V", Period = 202609, Quantity = 5600, NetAmount = 52040, YtdQuantity = 5600, YtdNetAmount = 52040, RatePercent = 1, YtdRebate = 20.40m, AccruedAmount = 20.40m, ToNextTier = 97960 }
        ]
    };

    [Fact]
    public void Statement_html_contains_totals_and_encodes_text()
    {
        string html = StatementRenderer.RenderHtml(SampleStatement(), new DateTime(2026, 10, 5, 4, 0, 0));
        Assert.Contains("Rebate statement 2026Q3", html);
        Assert.Contains("1,316.65", html);
        Assert.Contains("Coffee &lt;volume&gt; bonus", html);
        Assert.DoesNotContain("<volume>", html);
        Assert.Contains("TR-2026-001: 4,500 units to the next tier.", html);
        Assert.Contains("TR-2026-002: 97,960.00 EUR to the next tier.", html);
        Assert.Contains("FI************0785", html);
    }

    [Fact]
    public void Statement_csv_has_one_row_per_line()
    {
        string csv = StatementRenderer.RenderCsv(SampleStatement());
        string[] rows = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, rows.Length);
        Assert.Equal("17;2026Q3;KESPRO;TR-2026-001;202609;Q;5500;51850.00;5500;51850.00;2.5;1296.25;1296.25;EUR", rows[2]);
    }

    [Fact]
    public void Statement_files_are_written_below_distributor_and_quarter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string root = Path.Combine(Path.GetTempPath(), "tr-statements-" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new StatementFileWriter(Options.Create(new StatementOptions { Root = root }));
            string html = writer.Write(SampleStatement(), DateTime.Now);
            Assert.Equal(Path.Combine(root, "KESPRO", "2026Q3", "TR-2026Q3-KESPRO.html"), html);
            Assert.True(File.Exists(Path.ChangeExtension(html, ".csv")));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(html)!, "*.tmp"));

            // Re-rendering replaces the files.
            writer.Write(SampleStatement(), DateTime.Now);
            Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(html)!).Length);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("<StatementRequest StatementId=\"42\" Quarter=\"2026Q3\" RequestedBy=\"x\"/>", 42)]
    [InlineData("<StatementRequest Quarter=\"2026Q3\"/>", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Queue_message_statement_id(string? body, int expected)
    {
        Assert.Equal(expected, StatementQueueReader.ParseStatementId(body));
    }
}
