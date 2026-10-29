using FluentAssertions;
using VanArsdel.Notifications.Sending;
using Xunit;

namespace VanArsdel.UnitTests.Notifications;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Theory]
    [InlineData("FI", "fi")]
    [InlineData("se", "sv")]
    [InlineData("NO", "en")]
    [InlineData(null, "en")]
    public void Language_by_market(string market, string language)
    {
        TemplateRenderer.LanguageFor(market).Should().Be(language);
    }

    [Fact]
    public void Placeholders_are_filled_in_subject_and_body()
    {
        var mail = _renderer.Render("order-confirmed", "en", new Dictionary<string, object>
        {
            ["OrderNo"] = "W100234",
            ["Total"] = 129.9m,
            ["Currency"] = "EUR",
        });

        mail.Subject.Should().Contain("W100234");
        mail.Html.Should().Contain("(129.90 EUR)").And.NotContain("{{");
    }

    [Fact]
    public void Values_are_html_encoded_in_the_body_only()
    {
        var mail = _renderer.Render("order-confirmed", "en", new Dictionary<string, object> { ["OrderNo"] = "W1<b>" });

        mail.Subject.Should().EndWith("W1<b>");
        mail.Html.Should().Contain("W1&lt;b&gt;").And.NotContain("W1<b>");
    }

    [Fact]
    public void Missing_language_falls_back_to_english()
    {
        var english = _renderer.Render("order-cancelled", "en", new Dictionary<string, object> { ["OrderNo"] = "W1" });
        var finnish = _renderer.Render("order-cancelled", "fi", new Dictionary<string, object> { ["OrderNo"] = "W1" });

        finnish.Should().Be(english);
    }

    [Fact]
    public void Every_template_has_a_subject_line()
    {
        foreach (var name in new[] { "order-confirmed", "order-shipped", "ready-for-collection", "order-cancelled", "refund-issued", "return-registered", "tier-changed", "report-ready" })
        {
            _renderer.Render(name, "en", new Dictionary<string, object>()).Subject.Should().NotBeNullOrWhiteSpace(name);
        }
    }

    [Fact]
    public void Unknown_template_throws()
    {
        var act = () => _renderer.Render("no-such-template", "en", new Dictionary<string, object>());

        act.Should().Throw<InvalidOperationException>();
    }
}
