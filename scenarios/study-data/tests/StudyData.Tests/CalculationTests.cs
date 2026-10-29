using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudyData.Calculation;
using StudyData.Calculation.Development;
using StudyData.Core.Services;
using StudyData.Data.Seed;

namespace StudyData.Tests;

public class CalculationTests
{
    private static string Studies => Path.Combine(AppContext.BaseDirectory, "studies");

    [Fact]
    public void CommittedWorkbookMatchesValidationRecord() =>
        Assert.Equal(SampleStudy.WorkbookSha256, WorkbookStore.Hash(Path.Combine(Studies, "FAB-ONC-201", "calculations", SampleStudy.WorkbookFile)));

    [Fact]
    public void WorkbookWithAnotherHashIsRefused()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var study = SampleStudy.Create();
        Directory.CreateDirectory(Path.Combine(root, study.Code, "calculations"));
        File.WriteAllText(Path.Combine(root, study.Code, "calculations", SampleStudy.WorkbookFile), "changed");
        var store = new WorkbookStore(Options.Create(new CalculationOptions { WorkbookRoot = root, LocalCache = Path.Combine(root, "cache") }), NullLogger<WorkbookStore>.Instance);
        var ex = Assert.Throws<DerivationException>(() => store.GetVerifiedCopy(study, study.Workbooks[0]));
        Assert.Contains("validation record", ex.Message);
    }

    [Fact]
    public void VerifiedCopyIsCached()
    {
        var cache = Directory.CreateTempSubdirectory().FullName;
        var study = SampleStudy.Create();
        var store = new WorkbookStore(Options.Create(new CalculationOptions { WorkbookRoot = Studies, LocalCache = cache }), NullLogger<WorkbookStore>.Instance);
        var path = store.GetVerifiedCopy(study, study.Workbooks[0]);
        Assert.StartsWith(cache, path);
        Assert.Equal(SampleStudy.WorkbookSha256, WorkbookStore.Hash(path));
    }

    /// <summary>The development formulas reproduce the test cases of the workbook's validation sheet.</summary>
    [Fact]
    public async Task DevelopmentFormulasMatchWorkbookValidationCases()
    {
        using var wb = new XLWorkbook(Path.Combine(Studies, "FAB-ONC-201", "calculations", SampleStudy.WorkbookFile));
        var sheet = wb.Worksheet("Validation");
        var study = SampleStudy.Create();
        var service = new DevelopmentDerivationService();
        var cases = 0;
        foreach (var row in sheet.RowsUsed().Skip(1).Where(r => r.Cell(1).GetString().StartsWith("TC")))
        {
            var inputs = new Dictionary<string, object?>
            {
                ["HEIGHT_CM"] = (decimal)row.Cell(2).GetDouble(),
                ["WEIGHT_KG"] = (decimal)row.Cell(3).GetDouble(),
                ["SEX"] = row.Cell(4).GetString(),
                ["AGE_Y"] = (decimal)row.Cell(5).GetDouble(),
                ["SCR_UMOL"] = (decimal)row.Cell(6).GetDouble(),
                ["AUC_TARGET"] = (decimal)row.Cell(7).GetDouble(),
                ["DOSE_MG_KG"] = (decimal)row.Cell(8).GetDouble()
            };
            var result = await service.DeriveAsync(new DerivationRequest(study, study.Workbooks[0], inputs, new[] { "CRCL", "CARBO_DOSE", "FAB_DOSE", "EGFR" }));
            double V(string n) => double.Parse(result.Values[n]!, CultureInfo.InvariantCulture);
            Assert.Equal(row.Cell(9).GetDouble(), Math.Round(V("CRCL"), 1));
            Assert.Equal(row.Cell(10).GetDouble(), V("CARBO_DOSE"));
            Assert.Equal(row.Cell(11).GetDouble(), V("FAB_DOSE"));
            Assert.Equal(row.Cell(12).GetDouble(), Math.Round(V("EGFR")));
            cases++;
        }

        Assert.Equal(4, cases);
    }

    [Fact]
    public async Task BlankInputsGiveBlankOutputs()
    {
        var study = SampleStudy.Create();
        var result = await new DevelopmentDerivationService().DeriveAsync(new DerivationRequest(study, study.Workbooks[0],
            new Dictionary<string, object?> { ["WEIGHT_KG"] = 60m }, new[] { "BMI", "CARBO_DOSE" }));
        Assert.Null(result.Values["BMI"]);
        Assert.Null(result.Values["CARBO_DOSE"]);
    }

    [Fact]
    public void WorkbookHasEveryNamedRangeTheStudyUses()
    {
        using var wb = new XLWorkbook(Path.Combine(Studies, "FAB-ONC-201", "calculations", SampleStudy.WorkbookFile));
        var study = SampleStudy.Create();
        var names = wb.NamedRanges.Select(n => n.Name).ToHashSet();
        foreach (var input in study.Workbooks[0].Inputs)
        {
            Assert.Contains(input.Name, names);
        }

        foreach (var output in study.Forms.SelectMany(f => f.Fields).Where(f => f.WorkbookOutput is not null))
        {
            Assert.Contains(output.WorkbookOutput!, names);
        }
    }
}
