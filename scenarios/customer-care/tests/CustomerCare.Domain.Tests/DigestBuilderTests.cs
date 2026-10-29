using System;
using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class DigestBuilderTests
    {
        [Fact]
        public void Builds_summary()
        {
            var m = DigestBuilder.Build(new DateTime(2026, 10, 29), new[]
            {
                new QueueStats { QueueName = "Billing", Open = 10, Breached = 2, AverageResolveHours = 5.25 },
                new QueueStats { QueueName = "Field & <Network>", Open = 3 }
            }, "https://care.corp.local/");

            Assert.Equal("Customer Care daily summary 2026-10-29: 13 open, 2 breached", m.Subject);
            Assert.Contains("Field &amp; &lt;Network&gt;", m.HtmlBody);
            Assert.Contains("5.3", m.HtmlBody);
            Assert.Contains("https://care.corp.local/", m.HtmlBody);
            Assert.True(m.HtmlBody.IndexOf("Billing", StringComparison.Ordinal) < m.HtmlBody.IndexOf("Field &amp;", StringComparison.Ordinal));
        }
    }
}
