using System;
using Lucerne.ProjectSites.Domain;
using Xunit;

namespace Lucerne.ProjectSites.Tests
{
    public class DocumentNumberTests
    {
        [Fact]
        public void Formats_with_padded_sequence()
        {
            Assert.Equal("P19041-LUC-STR-DWG-0012", new DocumentNumber("P19041", "LUC", "STR", "DWG", 12).ToString());
        }

        [Theory]
        [InlineData("P19041-LUC-STR-DWG-0012", true)]
        [InlineData("p19041-luc-str-dwg-0012", true)]
        [InlineData("P19041-LUC-XXX-DWG-0012", false)]
        [InlineData("P19041-LUC-STR-ABC-0012", false)]
        [InlineData("STR-CALC-17", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Parses_only_standard_numbers(string text, bool valid)
        {
            DocumentNumber n;
            Assert.Equal(valid, DocumentNumber.TryParse(text, out n));
        }

        [Fact]
        public void Rejects_bad_project_and_sequence()
        {
            Assert.Throws<ArgumentException>(() => new DocumentNumber("19041", "LUC", "STR", "DWG", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentNumber("P19041", "LUC", "STR", "DWG", 0));
        }

        [Fact]
        public void Counter_key_is_per_discipline_and_type()
        {
            Assert.Equal("lucerne.counter.CIV.CAL", DocumentNumber.CounterKeyFor("CIV", "CAL"));
        }
    }

    public class RevisionRulesTests
    {
        [Theory]
        [InlineData(null, false, "P01")]
        [InlineData("P01", false, "P02")]
        [InlineData("P07", true, "C01")]
        [InlineData("C01", true, "C02")]
        public void Next_revision(string current, bool contractual, string expected)
        {
            Assert.Equal(expected, RevisionRules.Next(current, contractual));
        }

        [Fact]
        public void Contractual_cannot_go_back_to_preliminary()
        {
            Assert.Throws<InvalidOperationException>(() => RevisionRules.Next("C02", false));
            Assert.NotNull(RevisionRules.ValidateChange("C02", "P03"));
        }

        [Theory]
        [InlineData("P02", "P01")]
        [InlineData("P02", "X01")]
        [InlineData("C03", "C02")]
        public void Invalid_changes_are_refused(string before, string after)
        {
            Assert.NotNull(RevisionRules.ValidateChange(before, after));
        }

        [Theory]
        [InlineData(null, "P01")]
        [InlineData("P01", "P02")]
        [InlineData("P05", "C01")]
        [InlineData("C01", "C01")]
        public void Valid_changes_are_allowed(string before, string after)
        {
            Assert.Null(RevisionRules.ValidateChange(before, after));
        }
    }

    public class IsoWeekTests
    {
        [Theory]
        [InlineData("2019-12-30", "2020-W01")]
        [InlineData("2021-01-03", "2020-W53")]
        [InlineData("2018-01-01", "2018-W01")]
        [InlineData("2026-10-30", "2026-W44")]
        public void From_date(string date, string expected)
        {
            Assert.Equal(expected, IsoWeek.FromDate(DateTime.Parse(date)).ToString());
        }

        [Fact]
        public void Monday_and_arithmetic()
        {
            var w = IsoWeek.Parse("2020-W53");
            Assert.Equal(new DateTime(2020, 12, 28), w.Monday);
            Assert.Equal("2021-W01", w.AddWeeks(1).ToString());
            Assert.Equal("2020-W50", w.AddWeeks(-3).ToString());
            Assert.True(w.CompareTo(IsoWeek.Parse("2021-W01")) < 0);
        }

        [Fact]
        public void Weeks_in_year()
        {
            Assert.Equal(53, IsoWeek.WeeksInYear(2020));
            Assert.Equal(52, IsoWeek.WeeksInYear(2019));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IsoWeek(2019, 53));
        }
    }

    public class ClaimsTests
    {
        [Fact]
        public void Windows_login_round_trip()
        {
            Assert.Equal("i:0#.w|corp\\abrunner", Claims.WindowsLogin("CORP\\ABrunner"));
            Assert.Equal("i:0#.w|corp\\abrunner", Claims.WindowsLogin("i:0#.w|corp\\abrunner"));
            Assert.Equal("corp\\abrunner", Claims.AccountName("i:0#.w|corp\\abrunner"));
        }

        [Fact]
        public void Project_site_url()
        {
            Assert.Equal("/projects/p19041", Project.SiteRelativeUrl("P19041"));
            Assert.Throws<ArgumentException>(() => Project.SiteRelativeUrl("19041"));
        }
    }
}
