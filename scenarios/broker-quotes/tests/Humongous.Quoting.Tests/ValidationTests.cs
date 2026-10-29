using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Tests;

public class ValidationTests
{
    private readonly RiskValidator _validator = new();

    [Fact]
    public void Valid_risk_passes() => Assert.True(_validator.Validate(TestRisks.Cafe()).IsValid);

    [Theory]
    [InlineData("LS1 6HD", true)]
    [InlineData("ls16hd", true)]
    [InlineData("SW1A 1AA", true)]
    [InlineData("EC1A1BB", true)]
    [InlineData("12345", false)]
    [InlineData("LS1", false)]
    public void Validates_uk_postcodes(string postcode, bool valid) => Assert.Equal(valid, Postcode.IsValid(postcode));

    [Fact]
    public void Postcode_parts()
    {
        Assert.Equal("LS1 6HD", Postcode.Normalise(" ls16hd "));
        Assert.Equal("LS1", Postcode.District("LS1 6HD"));
        Assert.Equal("LS", Postcode.Area("LS1 6HD"));
        Assert.Equal("SW1A", Postcode.District("sw1a1aa"));
    }

    [Fact]
    public void Business_interruption_needs_material_damage()
    {
        var result = _validator.Validate(TestRisks.Cafe() with { MaterialDamage = null });
        Assert.Contains(result.Errors, e => e.PropertyName == "businessInterruption");
    }

    [Fact]
    public void Public_liability_limit_must_be_on_the_list()
    {
        var result = _validator.Validate(TestRisks.Cafe() with { PublicLiability = new PublicLiabilityCover { LimitOfIndemnity = 3_000_000m } });
        Assert.Contains(result.Errors, e => e.PropertyName == "publicLiability.limitOfIndemnity");
    }

    [Fact]
    public void At_least_one_section_is_needed()
    {
        var risk = TestRisks.Cafe() with { MaterialDamage = null, BusinessInterruption = null, EmployersLiability = null, PublicLiability = null };
        Assert.Contains(_validator.Validate(risk).Errors, e => e.PropertyName == "sections");
    }

    [Fact]
    public void Inception_window()
    {
        var today = new DateOnly(2024, 6, 1);
        Assert.Null(InceptionValidator.Check(today, today));
        Assert.NotNull(InceptionValidator.Check(today.AddDays(-1), today));
        Assert.NotNull(InceptionValidator.Check(today.AddDays(91), today));
    }

    [Fact]
    public void References_are_formatted()
    {
        Assert.Equal("HQ24-000042", References.Quote(2024, 42));
        Assert.Equal("HBC/000042/24", References.Policy(42, new DateOnly(2024, 4, 1)));
        Assert.Equal("HBC/000042/25", References.RenewedPolicy("HBC/000042/24", new DateOnly(2025, 4, 1)));
    }

    [Fact]
    public void Product_version_by_inception()
    {
        Assert.Equal("2023.2", ProductCatalogue.ForDate(new DateOnly(2024, 3, 31)).Version);
        Assert.Equal("2024.1", ProductCatalogue.ForDate(new DateOnly(2024, 4, 1)).Version);
        Assert.Throws<InvalidOperationException>(() => ProductCatalogue.ForDate(new DateOnly(2020, 1, 1)));
    }
}
