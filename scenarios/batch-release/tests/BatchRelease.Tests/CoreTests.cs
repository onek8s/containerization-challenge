using BatchRelease.Core;
using BatchRelease.Core.Certificates;
using BatchRelease.Core.Intake;
using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;
using BatchRelease.Core.Security;

namespace BatchRelease.Tests;

public class SpecificationTests
{
    [Theory]
    [InlineData(94.95, ResultOutcome.Pass)]   // rounds to 95.0
    [InlineData(94.94, ResultOutcome.Fail)]
    [InlineData(105.04, ResultOutcome.Pass)]
    [InlineData(105.05, ResultOutcome.Fail)]
    public void Numeric_results_are_rounded_to_the_limit_precision(double value, ResultOutcome expected)
    {
        var r = new LabResult { LowerLimit = 95.0m, UpperLimit = 105.0m, Value = (decimal)value };
        Assert.Equal(expected, SpecificationEvaluator.Evaluate(r));
    }

    [Fact]
    public void Text_results_keep_the_laboratory_outcome()
    {
        Assert.Equal(ResultOutcome.Pass, SpecificationEvaluator.Evaluate(new LabResult { TextResult = "Conforms", Outcome = ResultOutcome.Pass }));
        Assert.Equal(ResultOutcome.Pending, SpecificationEvaluator.Evaluate(new LabResult { Outcome = ResultOutcome.Pass }));
    }
}

public class ShelfLifeTests
{
    [Theory]
    [InlineData("2026-09-14", 36, "2029-08-31")]
    [InlineData("2026-01-31", 24, "2027-12-31")]
    [InlineData("2024-03-01", 12, "2025-02-28")]
    public void Expiry_is_end_of_month(string manufactured, int months, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), ShelfLife.ExpiryDate(DateOnly.Parse(manufactured), months));

    [Theory]
    [InlineData("2026-10-29", "2029-08-31", 34)]
    [InlineData("2026-10-29", "2026-11-28", 0)]
    [InlineData("2026-10-29", "2026-11-30", 1)]
    [InlineData("2026-10-29", "2026-10-01", 0)]
    public void Remaining_months(string today, string expiry, int expected) =>
        Assert.Equal(expected, ShelfLife.RemainingMonths(DateOnly.Parse(today), DateOnly.Parse(expiry)));
}

public class CertificateTests
{
    private static ReleaseCertificateContent Content(params CertificateDocument[] documents) => new()
    {
        CertificateNumber = "RC-TKU-2026-00042",
        Decision = "RELEASED",
        Manufacturer = "Proseware Pharmaceuticals Oy",
        SiteCode = "TKU",
        BatchNumber = "T2600418",
        ProductCode = "PRX-0450",
        ProductName = "Proxanol 450 mg \"film-coated\" tablets",
        Quantity = 412500m,
        Unit = "TAB",
        ManufacturedOn = new DateOnly(2026, 9, 14),
        ExpiresOn = new DateOnly(2029, 8, 31),
        QualifiedPerson = "Anna Virtanen",
        QualifiedPersonAccount = @"CORP\avirtanen",
        Meaning = SignatureMeanings.Release,
        SignedUtc = new DateTime(2026, 10, 29, 9, 15, 30, DateTimeKind.Utc),
        ReviewedBy = "Jari Koski",
        Documents = documents,
        Deviations = new[] { "DEV-26-0113", "DEV-26-0112" },
        KeyProvider = "Microsoft Platform Crypto Provider",
        HardwareKey = true,
    };

    [Fact]
    public void Serialization_is_independent_of_document_order()
    {
        var a = new CertificateDocument("ExecutedBatchRecord", "ebr.pdf", new string('A', 64));
        var b = new CertificateDocument("CertificateOfAnalysis", "coa.pdf", new string('b', 64));
        Assert.Equal(CertificateSerializer.Serialize(Content(a, b)), CertificateSerializer.Serialize(Content(b, a)));
    }

    [Fact]
    public void Serialized_certificate_can_be_read_back()
    {
        var json = CertificateSerializer.Serialize(Content());
        var text = System.Text.Encoding.UTF8.GetString(json);
        Assert.Contains("\"quantity\":\"412500\"", text);
        Assert.Contains("\"deviations\":[\"DEV-26-0112\",\"DEV-26-0113\"]", text);
        Assert.DoesNotContain("\n", text);

        var summary = CertificateSerializer.ReadSummary(json);
        Assert.Equal("RC-TKU-2026-00042", summary.CertificateNumber);
        Assert.Equal("T2600418", summary.BatchNumber);
        Assert.Equal(new DateOnly(2029, 8, 31), summary.ExpiresOn);
        Assert.Equal(new DateTime(2026, 10, 29, 9, 15, 30, DateTimeKind.Utc), summary.SignedUtc);
    }

    [Fact]
    public void Certificate_numbers()
    {
        Assert.Equal("RC-TKU-2026-00042", CertificateNumbers.Format(true, "tku", 2026, 42));
        Assert.Equal("RJ-TKU-2026-00001", CertificateNumbers.Format(false, "TKU", 2026, 1));
        Assert.True(CertificateNumbers.TryParse("RJ-TKU-2026-00007", out var release, out var site, out var year, out var seq));
        Assert.False(release);
        Assert.Equal(("TKU", 2026, 7), (site, year, seq));
        Assert.False(CertificateNumbers.TryParse("RC-TKU-26-7", out _, out _, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CertificateNumbers.Format(true, "TKU", 2026, 100000));
    }

    [Theory]
    [InlineData("T2600418", true)]
    [InlineData("T2600418B", true)]
    [InlineData("t2600418", false)]
    [InlineData("T260041", false)]
    public void Batch_numbers(string number, bool valid) => Assert.Equal(valid, BatchNumbers.IsValid(number));
}

public class ReleasePolicyTests
{
    [Fact]
    public void Builds_protected_dacl_with_deny_entries_first()
    {
        var sddl = new ReleasePolicyBuilder()
            .Allow("S-1-5-21-1-2-3-1101", ReleaseRights.ReadRecord | ReleaseRights.Review)
            .Allow("S-1-5-21-1-2-3-1102", ReleaseRights.ReadRecord | ReleaseRights.Release | ReleaseRights.Reject | ReleaseRights.Hold)
            .Deny("S-1-5-21-1-2-3-1199", ReleaseRights.Release)
            .Build();

        Assert.Equal("O:BAG:SYD:P(D;;0x4;;;S-1-5-21-1-2-3-1199)(A;;0x3;;;S-1-5-21-1-2-3-1101)(A;;0x1d;;;S-1-5-21-1-2-3-1102)", sddl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CORP\\QA")]
    [InlineData("S-1-")]
    public void Rejects_non_sids(string sid) =>
        Assert.Throws<ArgumentException>(() => new ReleasePolicyBuilder().Allow(sid, ReleaseRights.ReadRecord));

    [Fact]
    public void Rights_for_decisions()
    {
        Assert.Equal(ReleaseRights.Release, ReleaseRightsExtensions.RequiredFor(DecisionKind.Release));
        Assert.Equal(ReleaseRights.Hold, ReleaseRightsExtensions.RequiredFor(DecisionKind.Resume));
        Assert.Equal(new[] { "ReadRecord", "Review" }, (ReleaseRights.ReadRecord | ReleaseRights.Review).Names());
    }
}

public class SubmissionValidatorTests
{
    [Fact]
    public void Reports_all_problems()
    {
        var s = new BatchSubmission
        {
            BatchNumber = "X1",
            Quantity = 0,
            ManufacturedOn = new DateOnly(2026, 9, 1),
            ExpiresOn = new DateOnly(2026, 8, 1),
            Steps = { new StepSubmission { Sequence = 10 }, new StepSubmission { Sequence = 10 } },
            Results = { new ResultSubmission { TestCode = "ASSAY", LowerLimit = 5, UpperLimit = 1 }, new ResultSubmission { TestCode = "assay" } },
        };
        var errors = BatchSubmissionValidator.Validate(s);
        Assert.Equal(10, errors.Count);
    }
}
