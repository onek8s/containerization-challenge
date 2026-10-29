using System;
using System.Collections.Generic;
using Woodgrove.Statements.Core.Delivery;
using Xunit;

namespace Woodgrove.Statements.Tests
{
    public class DeliveryTests
    {
        [Fact]
        public void File_name_template_with_formats()
        {
            var values = FileNameTemplate.Values(new[] { new KeyValuePair<string, string>("StatementDate", "2026-09-30"), new KeyValuePair<string, string>("CustomerNo", "C10001") },
                                                 new DateTime(2026, 10, 5, 6, 0, 0));
            Assert.Equal("C10001_20260930.pdf", FileNameTemplate.Expand("{CustomerNo}_{StatementDate:yyyyMMdd}.pdf", values));
            Assert.Equal("Run_2026-10-05_0600.xlsx", FileNameTemplate.Expand("Run_{RunDate:yyyy-MM-dd_HHmm}.xlsx", values));
            Assert.Equal("Aged debt 05.10.2026", FileNameTemplate.ExpandText("Aged debt {RunDate:dd.MM.yyyy}", values));
        }

        [Fact]
        public void Unknown_fields_stay_visible_and_names_are_sanitised()
        {
            var values = new Dictionary<string, object> { { "Name", "A/B: C?" } };
            Assert.Equal("A_B_ C_ {Missing}.pdf", FileNameTemplate.Expand("{Name} {Missing}.pdf", values));
            Assert.Equal("_CON.pdf", FileNameTemplate.Sanitize("CON.pdf"));
            Assert.Equal("report", FileNameTemplate.Sanitize("report. "));
            Assert.Equal(120, FileNameTemplate.Sanitize(new string('x', 200) + ".pdf").Length);
            Assert.EndsWith(".pdf", FileNameTemplate.Sanitize(new string('x', 200) + ".pdf"));
        }

        [Fact]
        public void Recipients_are_split_deduplicated_and_validated()
        {
            var list = RecipientList.Parse("a@x.fi; b@x.fi,\r\nA@X.FI;;bad@;c@y");
            Assert.Equal(new[] { "a@x.fi", "b@x.fi", "bad@", "c@y" }, list);
            Assert.Equal(new[] { "bad@", "c@y" }, RecipientList.Invalid(list));
            Assert.Empty(RecipientList.Parse(null));
        }

        [Fact]
        public void Retry_backs_off_and_gives_up()
        {
            var policy = new RetryPolicy { MaxAttempts = 4, FirstDelay = TimeSpan.FromMinutes(10), MaxDelay = TimeSpan.FromMinutes(30) };
            var t = new DateTime(2026, 10, 1, 6, 0, 0);
            Assert.Equal(t.AddMinutes(10), policy.NextAttempt(1, t));
            Assert.Equal(t.AddMinutes(20), policy.NextAttempt(2, t));
            Assert.Equal(t.AddMinutes(30), policy.NextAttempt(3, t));
            Assert.Null(policy.NextAttempt(4, t));
        }
    }
}
