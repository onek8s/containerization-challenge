using BatchRelease.Core.Model;
using Microsoft.Extensions.Time.Testing;

namespace BatchRelease.Tests;

internal static class TestData
{
    public const string ReviewerSid = "S-1-5-21-1004336348-1177238915-682003330-2101";
    public const string QpSid = "S-1-5-21-1004336348-1177238915-682003330-2102";

    public static FakeTimeProvider Clock(int year = 2026, int month = 10, int day = 29) =>
        new(new DateTimeOffset(year, month, day, 9, 0, 0, TimeSpan.Zero));

    public static Product Product() => new()
    {
        Code = "PRX-0450",
        Name = "Proxanol 450 mg film-coated tablets",
        FamilyCode = "OSD",
        Strength = "450 mg",
        DosageForm = "Film-coated tablet",
        ShelfLifeMonths = 36,
        MinimumRemainingMonths = 18,
        Family = new ProductFamily { Code = "OSD", Name = "Oral solid dose", PolicySddl = "O:BAG:SYD:P(A;;0x1;;;AU)" },
    };

    public static Batch ReleasableBatch()
    {
        var product = Product();
        return new Batch
        {
            Id = 1,
            BatchNumber = "T2600418",
            ProductCode = product.Code,
            Product = product,
            SiteCode = "TKU",
            MesReference = "MES-778120",
            ManufacturedOn = new DateOnly(2026, 9, 14),
            ExpiresOn = new DateOnly(2029, 8, 31),
            Quantity = 412500,
            Unit = "TAB",
            Status = BatchStatus.Reviewed,
            Steps =
            {
                new RecordStep { Sequence = 10, Operation = "Dispensing", Status = StepStatus.Complete, PerformedBy = "mlahti", VerifiedBy = "jkoski" },
                new RecordStep { Sequence = 20, Operation = "Granulation", Status = StepStatus.Complete, PerformedBy = "mlahti", VerifiedBy = "jkoski" },
                new RecordStep { Sequence = 30, Operation = "Coating", Status = StepStatus.NotApplicable, Comment = "Uncoated sub-lot" },
            },
            Results =
            {
                new LabResult { TestCode = "ASSAY", TestName = "Assay", LowerLimit = 95.0m, UpperLimit = 105.0m, Value = 99.4m, Unit = "%", ApprovedBy = "lab.hanna" },
                new LabResult { TestCode = "DISS", TestName = "Dissolution", LowerLimit = 80m, Value = 92m, Unit = "%", ApprovedBy = "lab.hanna" },
            },
            Documents =
            {
                new BatchDocument { Kind = "ExecutedBatchRecord", FileName = "T2600418-ebr.pdf", Sha256 = new string('a', 64) },
                new BatchDocument { Kind = "CertificateOfAnalysis", FileName = "T2600418-coa.pdf", Sha256 = new string('b', 64) },
            },
            Decisions =
            {
                new Decision { Kind = DecisionKind.Review, SignerSid = ReviewerSid, SignerName = "Jari Koski", SignedUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) },
            },
        };
    }
}
