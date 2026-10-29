using System.Text;
using System.Text.RegularExpressions;
using ContosoFoods.TradeRebates.Rules;

namespace TradeRebates.Tests;

/// <summary>The sample files shipped for the lab must import cleanly against the reference data.</summary>
public class LabSampleTests
{
    static LabSampleTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static string Dir => Path.Combine(AppContext.BaseDirectory, "lab-samples");

    private static HashSet<string> CatalogueGtins() =>
        Regex.Matches(File.ReadAllText(Path.Combine(Dir, "13-reference-data.sql")), @"\('(\d{14})', N'")
             .Select(m => m.Groups[1].Value).ToHashSet();

    [Theory]
    [InlineData("kespro-sellout-202609.csv", 65001, "KESPRO", "CSV")]
    [InlineData("heinon-sellout-202609.so", 850, "HEINON", "SO")]
    public void Sample_files_parse_without_errors(string name, int codePage, string distributor, string format)
    {
        using var reader = new StreamReader(Path.Combine(Dir, name), Encoding.GetEncoding(codePage));
        ClaimFile file = ClaimFileParser.Parse(reader);
        Assert.Empty(file.Errors);
        Assert.Equal(format, file.Format);
        Assert.Equal(distributor, file.DistributorCode);
        Assert.Equal(202609, file.Period);
        Assert.NotEmpty(file.Lines);

        var catalogue = CatalogueGtins();
        Assert.Equal(12, catalogue.Count);
        Assert.All(file.Lines, l => Assert.Contains(l.Gtin, catalogue));
    }

    [Fact]
    public void Reference_data_gtins_have_valid_check_digits()
    {
        Assert.All(CatalogueGtins(), g => Assert.True(Gtin.IsValid(g), g));
    }

    [Fact]
    public void Reference_data_tier_schedules_parse()
    {
        string sql = File.ReadAllText(Path.Combine(Dir, "13-reference-data.sql"));
        var tiers = Regex.Matches(sql, @"N'([RS];[0-9.:;]+)'").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(4, tiers.Count);
        Assert.All(tiers, t => Assert.True(TierSchedule.TryParse(t, out _, out _), t));
    }
}
