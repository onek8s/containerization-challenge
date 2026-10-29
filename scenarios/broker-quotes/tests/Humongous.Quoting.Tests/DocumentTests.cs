using System.Text;
using Humongous.Documents.Rendering;
using Humongous.Documents.Wording;
using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data;
using Humongous.Quoting.Data.Entities;
using Humongous.Quoting.Domain;
using Microsoft.Extensions.Options;

namespace Humongous.Quoting.Tests;

public class DocumentTests
{
    private static DocumentComposer Composer() => new(new ClauseLibrary(), Options.Create(new DocumentOptions
    {
        FontFamily = OperatingSystem.IsWindows() ? "Arial" : "DejaVu Sans"
    }));

    [Fact]
    public void Clause_library_has_wording_for_rating_clauses()
    {
        var library = new ClauseLibrary();
        foreach (var code in new[] { "GEN01", "MD01", "MD12", "EL01", "PL01", "PL08" })
            Assert.NotEqual("Wording available from your underwriter.", library.Get(code).Text);
        Assert.Equal("Wording available from your underwriter.", library.Get("ZZ99").Text);
    }

    [Theory]
    [InlineData(DocumentType.PolicySchedule)]
    [InlineData(DocumentType.ElCertificate)]
    public void Renders_policy_documents_as_pdf(DocumentType type)
    {
        var premium = PremiumCalculator.Calculate(TestRisks.Rated((SectionCode.MaterialDamage, 400m), (SectionCode.EmployersLiability, 300m)), TestRisks.Product, 0.2m);
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "HBC/000007/24",
            AgencyCode = "ALP01",
            InceptionDate = new DateOnly(2024, 7, 1),
            ExpiryDate = new DateOnly(2025, 6, 30),
            ProductVersion = "2024.1",
            Risk = TestRisks.Cafe(),
            Premium = premium,
            Clauses = ["GEN01", "MD01", "EL01"],
            InsuredName = "Kettle & Crumb Ltd",
            Postcode = "LS1 6HD",
            TradeCode = "56102"
        };

        var (fileName, pdf) = Composer().Render(new DocumentModel(type, SampleData.Agencies().First(), null, policy, null, DateTime.UtcNow));

        Assert.StartsWith("HBC-000007-24-", fileName);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1000);
    }
}
