using System;
using System.Collections.Generic;
using System.IO;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents.Excel;
using Fabrikam.Housing.Documents.Import;
using FluentAssertions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using OfficeOpenXml;
using Xunit;

namespace Fabrikam.Housing.Tests.Documents
{
    public class WorkbookTests
    {
        private static readonly DateTime AsAt = new DateTime(2021, 6, 14);

        [Fact]
        public void Arrears_workbook_has_rows_total_and_formats()
        {
            var rows = new List<ArrearsRow>
            {
                new ArrearsRow { TenancyReference = "T020001", PropertyReference = "P01000", Address = "1 Barton Fields Road", Postcode = "BS5 6AB", PatchCode = "EAST", LeadTenant = "Olivia Smith", WeeklyCharge = 100m, Balance = 850m, Stage = 4, LastPayment = new DateTime(2021, 5, 1) },
                new ArrearsRow { TenancyReference = "T020002", PropertyReference = "P01001", Address = "2 Barton Fields Road", Postcode = "BS5 6AC", PatchCode = "EAST", LeadTenant = "George Jones", WeeklyCharge = 100m, Balance = 120m, Stage = 1 }
            };
            var bytes = new ReportWorkbookWriter().Arrears(rows, AsAt);
            using (var package = new ExcelPackage(new MemoryStream(bytes)))
            {
                var ws = package.Workbook.Worksheets["Arrears"];
                ws.Cells[1, 1].Text.Should().Contain("14/06/2021");
                ws.Cells[3, 1].Text.Should().Be("Tenancy");
                ws.Cells[4, 1].Text.Should().Be("T020001");
                ws.Cells[4, 9].GetValue<double>().Should().Be(8.5);
                ws.Cells[4, 10].Text.Should().Be("NoticeOfSeekingPossession");
                ws.Cells[6, 8].Formula.Should().Be("SUM(H4:H5)");
                ws.Cells[4, 8].Style.Numberformat.Format.Should().Be("£#,##0.00");
                ws.ConditionalFormatting.Count.Should().Be(2);
            }
        }

        [Fact]
        public void Performance_and_compliance_workbooks_open()
        {
            var writer = new ReportWorkbookWriter();
            var perf = writer.RepairsPerformance(new List<RepairsPerformanceRow>
            {
                new RepairsPerformanceRow { Priority = 1, ContractorCode = "AVONPH", Completed = 10, CompletedInTarget = 9, AverageDays = 0.6 }
            }, AsAt.AddDays(-30), AsAt);
            using (var p = new ExcelPackage(new MemoryStream(perf)))
            {
                var ws = p.Workbook.Worksheets["Performance"];
                ws.Cells[4, 1].Text.Should().Be("Emergency");
                ws.Cells[4, 5].GetValue<double>().Should().BeApproximately(0.9, 0.0001);
            }

            var compliance = writer.Compliance(new List<ComplianceRow>
            {
                new ComplianceRow { PropertyReference = "P01000", Address = "1 Barton", PatchCode = "EAST", Type = 1, InspectedOn = AsAt.AddDays(-370), ExpiresOn = AsAt.AddDays(-6), CertificateNumber = "CP12-1" },
                new ComplianceRow { PropertyReference = "P01001", Address = "2 Barton", PatchCode = "EAST", Type = 1, InspectedOn = AsAt.AddDays(-330), ExpiresOn = AsAt.AddDays(34), CertificateNumber = "CP12-2" },
                new ComplianceRow { PropertyReference = "P01002", Address = "3 Barton", PatchCode = "EAST", Type = 2, InspectedOn = AsAt.AddDays(-30), ExpiresOn = AsAt.AddYears(5), CertificateNumber = "EICR-3" }
            }, AsAt);
            using (var p = new ExcelPackage(new MemoryStream(compliance)))
            {
                var detail = p.Workbook.Worksheets["Certificates"];
                detail.Cells[4, 8].Text.Should().Be("Overdue");
                detail.Cells[5, 8].Text.Should().Be("DueSoon");
                detail.Cells[6, 8].Text.Should().Be("Compliant");
                p.Workbook.Worksheets["Summary"].Cells[4, 2].Formula.Should().Contain("COUNTIFS(Certificates!D:D,\"GasSafety\"");
            }
        }

        [Fact]
        public void Rent_roll_is_written_as_xls_in_the_fixed_column_layout()
        {
            var bytes = new ReportWorkbookWriter().RentRollXls(new List<RentRollRow>
            {
                new RentRollRow { PropertyReference = "P01000", Address = "1 Barton", EstateCode = "BARF", Bedrooms = 3, Status = 1, TenancyReference = "T020001", NetRent = 112.05m, ServiceCharge = 0m, WaterCharge = 7.42m },
                new RentRollRow { PropertyReference = "P01001", Address = "Flat 1", EstateCode = "MILL", Bedrooms = 1, Status = 2, NetRent = 89.40m, ServiceCharge = 14.05m }
            }, AsAt);
            Encoding(bytes).Should().Be("D0-CF-11-E0");
            var wb = new HSSFWorkbook(new MemoryStream(bytes));
            var sheet = wb.GetSheet("RentRoll");
            sheet.GetRow(0).GetCell(1).StringCellValue.Should().Be("14/06/2021");
            sheet.GetRow(2).GetCell(6).StringCellValue.Should().Be("NetRent");
            sheet.GetRow(3).GetCell(6).NumericCellValue.Should().Be(112.05);
            sheet.GetRow(4).GetCell(4).StringCellValue.Should().Be("VOID");
            sheet.GetRow(4).GetCell(5).StringCellValue.Should().BeEmpty();
            sheet.GetRow(3).GetCell(9).CellFormula.Should().Be("SUM(G4:I4)");
            sheet.GetRow(5).GetCell(6).CellFormula.Should().Be("SUM(G4:G5)");
        }

        private static string Encoding(byte[] bytes) => BitConverter.ToString(bytes, 0, 4);

        private static byte[] Schedule(bool xlsx)
        {
            IWorkbook wb = xlsx ? (IWorkbook)new XSSFWorkbook() : new HSSFWorkbook();
            var s = wb.CreateSheet("Schedule");
            s.CreateRow(0).CreateCell(0).SetCellValue("BRISTOL CITY COUNCIL - HOUSING BENEFIT");
            s.CreateRow(1).CreateCell(0).SetCellValue("Landlord payment schedule: FABRIKAM HOMES (LL 004412)");
            s.CreateRow(2).CreateCell(0).SetCellValue("BACS date 15/06/2021");
            var h = s.CreateRow(4);
            var headers = new[] { "Claim Ref", "Claimant Name", "Address", "Post Code", "Period From", "Period To", "Amount Paid" };
            for (var i = 0; i < headers.Length; i++) h.CreateCell(i).SetCellValue(headers[i]);

            var dateStyle = wb.CreateCellStyle();
            dateStyle.DataFormat = wb.CreateDataFormat().GetFormat("dd/mm/yyyy");

            var r1 = s.CreateRow(5);
            r1.CreateCell(0).SetCellValue("HB123456");
            r1.CreateCell(1).SetCellValue("Mrs Grace Hall");
            r1.CreateCell(2).SetCellValue("Flat 3, Millbrook Court");
            r1.CreateCell(3).SetCellValue("bs16 4aa");
            var c = r1.CreateCell(4); c.SetCellValue(new DateTime(2021, 5, 31)); c.CellStyle = dateStyle;
            c = r1.CreateCell(5); c.SetCellValue(new DateTime(2021, 6, 13)); c.CellStyle = dateStyle;
            r1.CreateCell(6).SetCellValue(180.36);

            var r2 = s.CreateRow(6);
            r2.CreateCell(0).SetCellValue("HB654321");
            r2.CreateCell(1).SetCellValue("Mr Leo Khan");
            r2.CreateCell(2).SetCellValue("4 Mead Lane");
            r2.CreateCell(3).SetCellValue("BS13 7QA");
            r2.CreateCell(4).SetCellValue("31/05/2021");
            r2.CreateCell(5).SetCellValue("13/06/2021");
            r2.CreateCell(6).SetCellValue("£1,012.50");

            var r3 = s.CreateRow(7);
            r3.CreateCell(0).SetCellValue("HB777777");
            r3.CreateCell(1).SetCellValue("Ms Ava Ward");
            r3.CreateCell(2).SetCellValue("9 Orchard Rise");
            r3.CreateCell(3).SetCellValue("BS14 0AA");
            r3.CreateCell(4).SetCellValue("17/05/2021");
            r3.CreateCell(5).SetCellValue("30/05/2021");
            r3.CreateCell(6).SetCellValue("(25.00)");

            s.CreateRow(9).CreateCell(0).SetCellValue("Total");
            using (var ms = new MemoryStream())
            {
                wb.Write(ms);
                return ms.ToArray();
            }
        }

        [Theory]
        [InlineData(false, "schedule.xls")]
        [InlineData(true, "schedule.xlsx")]
        public void Housing_benefit_schedule_is_read_from_council_layout(bool xlsx, string name)
        {
            var lines = new HousingBenefitScheduleReader().Read(new MemoryStream(Schedule(xlsx)), name);
            lines.Should().HaveCount(3);
            lines[0].ClaimReference.Should().Be("HB123456");
            lines[0].Postcode.Should().Be("BS16 4AA");
            lines[0].PeriodFrom.Should().Be(new DateTime(2021, 5, 31));
            lines[0].PeriodTo.Should().Be(new DateTime(2021, 6, 13));
            lines[0].Amount.Should().Be(180.36m);
            lines[0].RowNumber.Should().Be(6);
            lines[1].PeriodFrom.Should().Be(new DateTime(2021, 5, 31));
            lines[1].Amount.Should().Be(1012.50m);
            lines[2].Amount.Should().Be(-25.00m);
        }

        [Fact]
        public void Schedule_without_header_row_is_rejected()
        {
            var wb = new HSSFWorkbook();
            wb.CreateSheet("x").CreateRow(0).CreateCell(0).SetCellValue("nothing here");
            var ms = new MemoryStream();
            wb.Write(ms);
            Action act = () => new HousingBenefitScheduleReader().Read(new MemoryStream(ms.ToArray()), "bad.xls");
            act.Should().Throw<InvalidDataException>().WithMessage("*Claim Ref*");
        }
    }
}
