using System;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Configurator.Rules;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Catalog;
using Xunit;

namespace Tailspin.DealerOrders.Tests
{
    public class ConfiguratorTests
    {
        private static readonly DateTime Today = new DateTime(2019, 10, 15);
        private readonly FileCatalog _catalog = new FileCatalog(Lab.CatalogFolder);
        private readonly ConfigurationEngine _engine = new ConfigurationEngine();

        [Fact]
        public void All_catalog_models_load_and_their_rules_compile()
        {
            var models = _catalog.All();
            Assert.Equal(4, models.Count);
            foreach (var model in models)
                Assert.NotEmpty(_engine.CompileRules(model));
            Assert.Equal("Hydraulic filter element", _catalog.PartDescription("P-10045"));
        }

        [Theory]
        [InlineData("A and B or C", "((A and B) or C)")]
        [InlineData("A or B and C", "(A or (B and C))")]
        [InlineData("not A and (B or C)", "(not A and (B or C))")]
        [InlineData("A && !B || market(us, ca)", "((A and not B) or market(US, CA))")]
        public void Rule_conditions_parse_with_and_binding_tighter_than_or(string text, string expected)
        {
            Assert.Equal(expected, RuleParser.ParseConditionOnly(text).ToString());
        }

        [Fact]
        public void Rule_with_any_and_message_parses()
        {
            var rule = RuleParser.Parse("R1", "LOAD-VAR => requires any(CRANE-11M, ENG-240) \"needs a bigger crane\"");
            Assert.Equal(ConsequenceKind.RequiresAny, rule.Consequence.Kind);
            Assert.Equal(new[] { "CRANE-11M", "ENG-240" }, rule.Consequence.Options);
            Assert.Equal("needs a bigger crane", rule.Consequence.Message);
        }

        [Theory]
        [InlineData("A and => requires B")]
        [InlineData("A => wants B")]
        [InlineData("(A or B => requires C")]
        [InlineData("A => requires B extra")]
        public void Bad_rules_are_rejected(string text)
        {
            Assert.Throws<RuleSyntaxException>(() => RuleParser.Parse("R", text));
        }

        [Fact]
        public void Defaults_are_added_for_required_groups()
        {
            var outcome = _engine.Resolve(_catalog.Get("TF14"), new string[0], "FI", Today);
            Assert.True(outcome.IsValid, string.Join(" ", outcome.Errors));
            Assert.Contains("ENG-205", outcome.Configuration.Options);
            Assert.Contains("CRANE-10M", outcome.Configuration.Options);
            Assert.Contains("TYRE-710", outcome.Added);
        }

        [Fact]
        public void Requires_rule_reports_missing_option()
        {
            var outcome = _engine.Resolve(_catalog.Get("TF14"), new[] { "CRANE-11M" }, "FI", Today);
            Assert.False(outcome.IsValid);
            Assert.Contains("The 11 m crane needs the heavy duty bogie axle.", outcome.Errors);
        }

        [Fact]
        public void Includes_rule_replaces_default_in_single_choice_group()
        {
            var outcome = _engine.Resolve(_catalog.Get("TF14"), new[] { "WINCH", "CRANE-11M" }, "SE", Today);
            Assert.True(outcome.IsValid, string.Join(" ", outcome.Errors));
            Assert.Contains("BOGIE-HD", outcome.Configuration.Options);
            Assert.DoesNotContain("BOGIE-STD", outcome.Configuration.Options);
            Assert.Contains(outcome.Warnings, w => w.Contains("adds 2 weeks"));
        }

        [Fact]
        public void Condition_rules_enforce_market_specific_engines()
        {
            var model = _catalog.Get("TF14");
            Assert.Contains(_engine.Resolve(model, new string[0], "US", Today).Errors, e => e.Contains("Tier 4 Final"));
            Assert.True(_engine.Resolve(model, new[] { "ENG-T4F" }, "US", Today).IsValid);
            var europe = _engine.Resolve(model, new[] { "ENG-T4F" }, "DE", Today);
            Assert.Contains(europe.Errors, e => e.Contains("only for the USA and Canada"));
        }

        [Fact]
        public void Excludes_and_option_market_restrictions_are_reported()
        {
            var model = _catalog.Get("TF14");
            var outcome = _engine.Resolve(model, new[] { "TRACKS-ECO", "TYRE-780", "COLD-PACK" }, "DE", Today);
            Assert.Contains("Bogie tracks Eco do not fit 780 mm tyres.", outcome.Errors);
            Assert.Contains(outcome.Errors, e => e.Contains("COLD-PACK") && e.Contains("market DE"));
        }

        [Fact]
        public void Harvester_in_nordics_needs_fire_suppression_and_h560_brings_pro_control()
        {
            var model = _catalog.Get("TH8");
            Assert.Contains(_engine.Resolve(model, new string[0], "SE", Today).Errors, e => e.Contains("Fire suppression"));
            var outcome = _engine.Resolve(model, new[] { "HEAD-H560", "CRANE-11M", "FIRE-SUPP" }, "SE", Today);
            Assert.True(outcome.IsValid, string.Join(" ", outcome.Errors));
            Assert.Contains("CTRL-PRO", outcome.Added);
        }

        [Fact]
        public void Unknown_option_is_an_error()
        {
            var outcome = _engine.Resolve(_catalog.Get("TL450"), new[] { "JETPACK" }, "FI", Today);
            Assert.Contains("Option JETPACK does not exist for Tailspin TL450 compact loader.", outcome.Errors);
        }

        [Fact]
        public void Configuration_id_is_stable_and_order_independent()
        {
            var a = new MachineConfiguration("TF14", new[] { "CRANE-11M", "BOGIE-HD", "led-pack" });
            var b = new MachineConfiguration("TF14", new[] { "LED-PACK", "CRANE-11M", "BOGIE-HD" });
            Assert.Equal(a.ConfigurationId, b.ConfigurationId);
            Assert.Equal(10, a.ConfigurationId.Length);
            Assert.StartsWith("TF14", a.ConfigurationId);
            Assert.NotEqual(a.ConfigurationId, new MachineConfiguration("TF14", new[] { "CRANE-11M" }).ConfigurationId);
        }

        [Fact]
        public void Lead_time_skips_summer_shutdown_and_ships_on_thursday()
        {
            var calc = new LeadTimeCalculator();
            var date = calc.EarliestDelivery(new DateTime(2019, 5, 20), 8);
            Assert.Equal(DayOfWeek.Thursday, date.DayOfWeek);
            // 8 production weeks from week 21 run past the shutdown weeks 28-31.
            Assert.True(LeadTimeCalculator.IsoWeek(date) >= 33, date.ToString("yyyy-MM-dd"));
            Assert.Equal(1, LeadTimeCalculator.IsoWeek(new DateTime(2019, 12, 30)));
        }
    }
}
