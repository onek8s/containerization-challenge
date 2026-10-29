using System;
using System.Collections.Generic;
using Northwind.Inspections.Core;
using Xunit;

namespace Northwind.Inspections.Core.Tests
{
    public class ReportTests
    {
        private static Inspection Sample()
        {
            return new Inspection
            {
                Id = 7,
                ClaimNumber = "HO-2024-004512",
                PolicyHolder = "M. O'Brien & Sons",
                PropertyAddress = "12 Harbour Road, Cork",
                PerilCode = "WAT",
                InspectedOn = new DateTime(2024, 5, 17),
                AdjusterName = "Aoife Kelly",
                HandlerEmail = "claims.cork@northwind.example",
                Status = InspectionStatus.Submitted,
                EstimatedLoss = 18250m,
                Notes = "Ceiling collapse in kitchen.\nDamp in hallway.",
                CreatedUtc = new DateTime(2024, 5, 17, 9, 0, 0)
            };
        }

        [Fact]
        public void BuildsEncodedHtml()
        {
            var report = new InspectionReport
            {
                Inspection = Sample(),
                Photos = new List<Photo> { new Photo { Id = Guid.Empty, OriginalFileName = "<kitchen>.jpg", Caption = "Ceiling" } },
                ContactSheetContentId = "sheet",
                GeneratedUtc = new DateTime(2024, 5, 18, 7, 30, 0)
            };
            var html = ReportBuilder.Html(report, "https://inspections.corp.local/");
            Assert.Contains("M. O&#39;Brien &amp; Sons", html);
            Assert.Contains("&lt;kitchen&gt;.jpg", html);
            Assert.Contains("Escape of water", html);
            Assert.Contains("€18,250", html);
            Assert.Contains("cid:sheet", html);
            Assert.Contains("https://inspections.corp.local/media/00000000000000000000000000000000", html);
            Assert.Contains("Ceiling collapse in kitchen.<br/>Damp in hallway.", html);
        }

        [Fact]
        public void BuildsSubject()
        {
            Assert.Equal("Inspection report HO-2024-004512 - 12 Harbour Road, Cork", ReportBuilder.Subject(Sample()));
        }

        [Fact]
        public void BacksOffAndGivesUp()
        {
            var now = new DateTime(2024, 1, 1, 12, 0, 0);
            Assert.Equal(now.AddMinutes(2), ReportSchedule.NextAttemptUtc(1, now));
            Assert.Equal(now.AddMinutes(30), ReportSchedule.NextAttemptUtc(4, now));
            Assert.Null(ReportSchedule.NextAttemptUtc(ReportSchedule.MaxAttempts, now));
        }

        [Fact]
        public void PurgesOnlyReportedInspectionsAfterRetention()
        {
            var i = Sample();
            var now = new DateTime(2024, 9, 1);
            Assert.False(ReportSchedule.IsPurgeDue(i, now, 90));
            i.Status = InspectionStatus.ReportSent;
            i.ReportSentUtc = new DateTime(2024, 6, 1);
            Assert.True(ReportSchedule.IsPurgeDue(i, now, 90));
            Assert.False(ReportSchedule.IsPurgeDue(i, now, 120));
            Assert.False(ReportSchedule.IsPurgeDue(i, now, 0));
        }

        [Fact]
        public void ChecksReportPreconditions()
        {
            var i = Sample();
            string reason;
            Assert.True(ReportSchedule.CanRequestReport(i, 3, out reason));
            Assert.False(ReportSchedule.CanRequestReport(i, 0, out reason));
            i.Status = InspectionStatus.Draft;
            Assert.False(ReportSchedule.CanRequestReport(i, 3, out reason));
            Assert.Contains("Submit", reason);
        }

        [Fact]
        public void BuildsWatermark()
        {
            Assert.Equal("HO-2024-004512  #03  2024-05-17 14:03  Aoife Kelly",
                Watermark.Text("ho2024004512", new DateTime(2024, 5, 17, 14, 3, 9), 3, "Aoife Kelly"));
            Assert.Equal("abc\u2026", Watermark.Caption("abcdef", 4));
        }
    }
}
