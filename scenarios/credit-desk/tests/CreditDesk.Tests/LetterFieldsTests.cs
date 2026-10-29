using System;
using CreditDesk.Core;
using Xunit;

namespace CreditDesk.Tests
{
    public class LetterFieldsTests
    {
        private static LetterMessage Message() => new LetterMessage
        {
            RequestId = 4211,
            AccountNumber = "C10442",
            CustomerName = "Alpine Hardware GmbH",
            ContactName = "Ms. Huber",
            AddressLine1 = "Industriestrasse 12",
            PostalCode = "6020",
            City = "Innsbruck",
            CountryCode = "at",
            PreviousLimit = 20000m,
            NewLimit = 30000m,
            Approved = true,
            DecidedAt = new DateTime(2024, 3, 15, 10, 0, 0),
            ValidUntil = new DateTime(2025, 3, 15),
            SignedBy = "Dana Whitfield",
            SignedByTitle = "Credit Controller"
        };

        [Fact]
        public void Build_fills_every_template_bookmark()
        {
            var values = LetterFields.Build(Message(), new DateTime(2024, 3, 16));
            foreach (var bookmark in LetterFields.Bookmarks)
                Assert.True(values.ContainsKey(bookmark), bookmark);
        }

        [Fact]
        public void Approved_increase_uses_new_limit()
        {
            var values = LetterFields.Build(Message(), new DateTime(2024, 3, 16));
            Assert.Equal("30,000.00", values["NewLimit"]);
            Assert.Contains("increased to 30,000.00", values["DecisionText"]);
            Assert.Equal("3/15/2025", values["ValidUntil"]);
        }

        [Fact]
        public void Rejection_keeps_previous_limit_and_appends_comment()
        {
            var m = Message();
            m.Approved = false;
            m.DecisionComment = "Please contact us again after six months of on-time payments.";
            var values = LetterFields.Build(m, new DateTime(2024, 3, 16));
            Assert.Equal("20,000.00", values["NewLimit"]);
            Assert.Equal("", values["ValidUntil"]);
            Assert.EndsWith("on-time payments.", values["DecisionText"]);
        }

        [Fact]
        public void Address_block_uses_line_breaks_and_country()
        {
            Assert.Equal("Alpine Hardware GmbH\vIndustriestrasse 12\v6020 Innsbruck\vAT", LetterFields.AddressBlock(Message()));
        }

        [Fact]
        public void Reference_and_file_name()
        {
            var m = Message();
            m.AccountNumber = "C/10442";
            Assert.Equal("CD-2024-004211", LetterFields.Reference(m));
            Assert.Equal("C_10442_CD-2024-004211_20240315.pdf", LetterFields.FileName(m));
        }

        [Fact]
        public void Missing_contact_uses_department()
        {
            var m = Message();
            m.ContactName = null;
            Assert.Equal("Accounts Payable", LetterFields.Build(m, DateTime.Today)["ContactName"]);
        }
    }
}
