using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Api.Tests.Support;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Jobs;
using Fabrikam.Housing.Jobs.Jobs;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using FluentAssertions;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NPOI.HSSF.UserModel;
using Xunit;

namespace Fabrikam.Housing.Api.Tests.Jobs
{
    public class JobsTests
    {
        [Fact]
        public void Every_job_is_registered_with_its_schedule()
        {
            var storage = new MemoryStorage();
            var manager = new RecurringJobManager(storage);
            JobCatalog.RegisterAll(manager, new JobsOptions { TimeZone = "Europe/London" });
            using (var connection = storage.GetConnection())
            {
                var jobs = connection.GetRecurringJobs();
                jobs.Select(j => j.Id).Should().BeEquivalentTo(JobCatalog.All.Select(e => e.Name));
                jobs.Single(j => j.Id == "rent-charge-run").Cron.Should().Be("30 0 * * 1");
            }
            using (var harness = new JobsHarness())
            {
                foreach (var entry in JobCatalog.All)
                    using (var scope = harness.CreateScope())
                        scope.ServiceProvider.GetRequiredService(entry.JobType).Should().NotBeNull();
            }
        }

        [Fact]
        public void Rent_run_charges_the_week_once_and_skips_rent_free_weeks()
        {
            using (var h = new JobsHarness())
            {
                int Charges(DateTime week)
                {
                    using (var db = h.CreateDbContext())
                        return db.RentTransactions.Count(t => t.RentWeek == week && t.Type == TransactionType.RentCharge);
                }

                var nextWeek = new DateTime(2021, 6, 21);
                Charges(nextWeek).Should().Be(0);
                h.Clock.Now = nextWeek.AddMinutes(30);
                h.Run<RentChargeRunJob>(j => j.RunAsync());
                int current;
                using (var db = h.CreateDbContext())
                    current = db.Tenancies.Count(t => t.Status != Tenancies.TenancyStatus.Ended);
                Charges(nextWeek).Should().Be(current);

                h.Run<RentChargeRunJob>(j => j.RunAsync());
                Charges(nextWeek).Should().Be(current);

                var christmas = new DateTime(2021, 12, 20);
                h.Clock.Now = christmas.AddMinutes(30);
                h.Run<RentChargeRunJob>(j => j.RunAsync());
                Charges(christmas).Should().Be(0);
            }
        }

        private class RecordingGateway : IPaymentsGateway
        {
            public List<(string Mandate, decimal Amount, DateTime Date, string Key)> Created { get; } = new List<(string, decimal, DateTime, string)>();
            public Task<RedirectFlow> StartMandateAsync(string a, string b, string c, string d, string e) => throw new NotSupportedException();
            public Task<CompletedMandate> CompleteMandateAsync(string a, string b) => throw new NotSupportedException();
            public Task CancelMandateAsync(string a) => throw new NotSupportedException();
            public Task<string> CreatePaymentAsync(string mandateId, decimal amount, DateTime chargeDate, string reference, string key)
            {
                Created.Add((mandateId, amount, chargeDate, key));
                return Task.FromResult("PMTEST" + Created.Count);
            }
            public Task<CardPaymentSession> StartCardPaymentAsync(string a, decimal b, string c, string d) => throw new NotSupportedException();
            public Task<CardPaymentResult> GetCardPaymentAsync(string a) => Task.FromResult<CardPaymentResult>(null);
            public Task<IList<PaymentEvent>> ListEventsAsync(DateTime since) => Task.FromResult<IList<PaymentEvent>>(new List<PaymentEvent>());
        }

        [Fact]
        public void Direct_debit_collections_are_submitted_only_when_due()
        {
            var gateway = new RecordingGateway();
            using (var h = new JobsHarness(s => s.AddSingleton<IPaymentsGateway>(gateway)))
            {
                var calendar = new WorkingCalendar();
                var due = calendar.AddWorkingDays(JobsHarness.Today, 3).Date;
                int dueId, laterId;
                using (var db = h.CreateDbContext())
                {
                    foreach (var m in db.Mandates) m.NextCollectionDate = JobsHarness.Today.AddDays(30);
                    var monthly = db.Mandates.Where(m => m.Status == MandateStatus.Active && m.Frequency == CollectionFrequency.Monthly).Take(2).ToList();
                    monthly[0].NextCollectionDate = due;
                    monthly[1].NextCollectionDate = calendar.AddWorkingDays(JobsHarness.Today, 8).Date;
                    dueId = monthly[0].Id;
                    laterId = monthly[1].Id;
                    db.SaveChanges();
                }

                h.Run<DirectDebitCollectionsJob>(j => j.RunAsync());
                gateway.Created.Should().HaveCount(1);
                gateway.Created[0].Date.Should().Be(due);

                using (var db = h.CreateDbContext())
                {
                    var payment = db.DirectDebitPayments.Single(p => p.ProviderPaymentId == "PMTEST1");
                    payment.MandateId.Should().Be(dueId);
                    payment.Status.Should().Be(PaymentStatus.Submitted);
                    db.Mandates.Find(dueId).NextCollectionDate.Should().BeAfter(due.AddDays(20));
                    db.Mandates.Find(laterId).NextCollectionDate.Should().Be(calendar.AddWorkingDays(JobsHarness.Today, 8).Date);
                }

                h.Run<DirectDebitCollectionsJob>(j => j.RunAsync());
                gateway.Created.Should().HaveCount(1);
            }
        }

        [Fact]
        public void Arrears_review_texts_new_cases_and_prints_first_letters()
        {
            using (var h = new JobsHarness())
            {
                int smsTenancy, letterTenancy;
                using (var db = h.CreateDbContext())
                {
                    var balances = db.RentTransactions.AsEnumerable().GroupBy(t => t.TenancyId).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
                    var withCase = db.ArrearsCases.Select(c => c.TenancyId).ToHashSet();
                    var candidates = db.Tenancies.Include(t => t.Tenants).AsEnumerable()
                        .Where(t => !t.HousingBenefitClaimed && !t.UniversalCredit && !withCase.Contains(t.Id)
                            && t.Tenants.All(x => !x.Vulnerable) && t.Tenants.Any(x => x.IsLeadTenant && x.MobilePhone != null))
                        .Where(t => balances.TryGetValue(t.Id, out var b) && b < 5)
                        .Take(2).ToList();
                    smsTenancy = candidates[0].Id;
                    letterTenancy = candidates[1].Id;
                    foreach (var t in candidates)
                        db.RentTransactions.Add(new RentTransaction { TenancyId = t.Id, PostedOn = JobsHarness.Today.AddDays(-40), RentWeek = RentCalendar.WeekStart(JobsHarness.Today.AddDays(-40)), Type = TransactionType.Adjustment, Amount = t.WeeklyCharge * 3 - balances[t.Id], Description = "Test arrears" });
                    db.ArrearsCases.Add(new ArrearsCase { TenancyId = letterTenancy, Stage = ArrearsStage.Reminder, BalanceAtStage = 100, StageReachedOn = JobsHarness.Today.AddDays(-20), CreatedAt = JobsHarness.Today.AddDays(-20) });
                    db.SaveChanges();
                }

                h.Run<ArrearsReviewJob>(j => j.RunAsync());

                using (var db = h.CreateDbContext())
                {
                    db.ArrearsCases.Single(c => c.TenancyId == smsTenancy && c.ClosedOn == null).Stage.Should().Be(ArrearsStage.Reminder);
                    db.Communications.Should().Contain(c => c.TenancyId == smsTenancy && c.Channel == Channel.Sms && c.TemplateCode == "ArrearsReminder" && c.Status == CommunicationStatus.Sent);
                    db.ArrearsCases.Single(c => c.TenancyId == letterTenancy && c.ClosedOn == null).Stage.Should().Be(ArrearsStage.FirstLetter);
                    db.Communications.Should().Contain(c => c.TenancyId == letterTenancy && c.Channel == Channel.Letter && c.TemplateCode == "ArrearsStage2");
                    var reference = db.Tenancies.Find(letterTenancy).Reference;
                    var pdf = h.Files("print", "20210614").Single(f => f.EndsWith("ArrearsStage2_" + reference + ".pdf"));
                    Encoding.ASCII.GetString(File.ReadAllBytes(pdf).Take(5).ToArray()).Should().Be("%PDF-");
                }
                h.Files("simulators", "sms").Should().NotBeEmpty();
                h.Documents.Uploads.Should().Contain(u => u.Upload.TenancyId == letterTenancy);
            }
        }

        [Fact]
        public void Finance_export_writes_a_balanced_windows_1252_journal_once()
        {
            using (var h = new JobsHarness())
            {
                h.Clock.Now = JobsHarness.Today.AddHours(22);
                h.Run<FinanceExportJob>(j => j.RunAsync());

                var file = h.Files("finance").Single();
                Path.GetFileName(file).Should().Be("FAB_RENT_20210614.csv");
                var bytes = File.ReadAllBytes(file);
                bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
                var lines = Encoding.GetEncoding(1252).GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                lines[0].Should().Be("Date,Nominal,CostCentre,Reference,Narrative,Debit,Credit");
                var debit = lines.Skip(1).Sum(l => decimal.Parse(l.Split(',')[5], System.Globalization.CultureInfo.InvariantCulture));
                var credit = lines.Skip(1).Sum(l => decimal.Parse(l.Split(',')[6], System.Globalization.CultureInfo.InvariantCulture));
                debit.Should().Be(credit);
                lines.Should().Contain(l => l.Contains(",5100,")).And.Contain(l => l.Contains(",4000,"));

                using (var db = h.CreateDbContext())
                {
                    db.RentTransactions.Count(t => t.JournalBatchId == null && t.PostedOn <= h.Clock.Now).Should().Be(0);
                    db.RentTransactions.Count(t => t.JournalBatchId == null).Should().BeGreaterThan(0, "housing benefit for this week is posted ahead");
                    db.JournalBatches.Single().Status.Should().Be("Written");
                }

                h.Run<FinanceExportJob>(j => j.RunAsync());
                h.Files("finance").Should().HaveCount(1);
            }
        }

        [Fact]
        public void Contractor_payment_run_sends_a_standard_18_file_and_remittances()
        {
            using (var h = new JobsHarness())
            {
                int approved;
                using (var db = h.CreateDbContext())
                    approved = db.ContractorInvoices.Count(i => i.Status == InvoiceStatus.Approved);
                approved.Should().BeGreaterThan(0);

                h.Clock.Now = new DateTime(2021, 6, 17, 14, 0, 0);
                h.Run<ContractorPaymentRunJob>(j => j.RunAsync());

                var file = h.Files("simulators", "bacs").Single();
                Path.GetFileName(file).Should().Be("FAB000042.txt");
                var records = File.ReadAllText(file).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                records.Should().OnlyContain(r => r.Length == 100);
                records[0].Should().StartWith("VOL1000042");
                records.Last().Should().StartWith("UTL1");
                h.Files("bacs-sent").Should().ContainSingle();

                using (var db = h.CreateDbContext())
                {
                    db.ContractorInvoices.Count(i => i.Status == InvoiceStatus.Approved).Should().Be(0);
                    var run = db.PaymentRuns.Single();
                    run.Status.Should().Be("Submitted");
                    run.ProcessingDate.Should().Be(new DateTime(2021, 6, 22));
                    records.Count(r => r.Substring(15, 2) == "99").Should().Be(run.PaymentCount);
                    db.Communications.Count(c => c.TemplateCode == "ContractorRemittance").Should().Be(run.PaymentCount);
                }
                h.Files("simulators", "mail").Should().HaveCountGreaterOrEqualTo(1);
            }
        }

        [Fact]
        public void Housing_benefit_schedule_is_matched_by_claim_and_by_address()
        {
            using (var h = new JobsHarness())
            {
                Tenancies.Tenancy byClaim, byAddress;
                using (var db = h.CreateDbContext())
                {
                    byClaim = db.Tenancies.Include(t => t.Property).First(t => t.HousingBenefitClaimRef != null);
                    byAddress = db.Tenancies.Include(t => t.Property).First(t => t.HousingBenefitClaimRef == null && t.Status == Tenancies.TenancyStatus.Current);
                }

                Directory.CreateDirectory(h.Folder.Sub("hb"));
                var path = h.Folder.Sub("hb", "BCC_HB_LANDLORD_20210611.xls");
                var workbook = new HSSFWorkbook();
                var sheet = workbook.CreateSheet("Schedule");
                sheet.CreateRow(0).CreateCell(0).SetCellValue("Bristol City Council - Housing Benefit landlord payments");
                sheet.CreateRow(1).CreateCell(0).SetCellValue("Payment run 11/06/2021  BACS date 15/06/2021");
                var header = sheet.CreateRow(3);
                var titles = new[] { "Claim Ref", "Claimant", "Address", "Postcode", "Period From", "Period To", "Amount" };
                for (var i = 0; i < titles.Length; i++) header.CreateCell(i).SetCellValue(titles[i]);
                void Row(int r, string claim, string name, string address, string postcode, decimal amount)
                {
                    var row = sheet.CreateRow(r);
                    row.CreateCell(0).SetCellValue(claim);
                    row.CreateCell(1).SetCellValue(name);
                    row.CreateCell(2).SetCellValue(address);
                    row.CreateCell(3).SetCellValue(postcode);
                    row.CreateCell(4).SetCellValue("31/05/2021");
                    row.CreateCell(5).SetCellValue("13/06/2021");
                    row.CreateCell(6).SetCellValue((double)amount);
                }
                Row(4, byClaim.HousingBenefitClaimRef, "Claimant One", byClaim.Property.AddressLine1, byClaim.Property.Postcode, 182.40m);
                Row(5, "HB000001", "Claimant Two", byAddress.Property.AddressLine1.ToUpperInvariant(), byAddress.Property.Postcode.Replace(" ", ""), 150.00m);
                Row(6, "HB999999", "Nobody Known", "1 Nowhere Street", "BS99 9ZZ", 75.25m);
                Row(8, "Total", "", "", "", 407.65m);
                using (var fs = File.Create(path)) workbook.Write(fs);

                h.Run<HousingBenefitImportJob>(j => j.RunAsync());

                File.Exists(path).Should().BeFalse();
                h.Files("hb", "Processed").Should().ContainSingle(f => f.EndsWith("BCC_HB_LANDLORD_20210611.xls"));
                using (var db = h.CreateDbContext())
                {
                    var import = db.HousingBenefitImports.Single();
                    import.Lines.Should().Be(3);
                    import.Matched.Should().Be(2);
                    import.Unmatched.Should().Contain("HB999999");
                    db.RentTransactions.Should().Contain(t => t.TenancyId == byClaim.Id && t.Type == TransactionType.HousingBenefit && t.Amount == -182.40m && t.RentWeek == new DateTime(2021, 5, 31));
                    db.RentTransactions.Should().Contain(t => t.TenancyId == byAddress.Id && t.Type == TransactionType.HousingBenefit && t.Amount == -150.00m);
                    db.Tenancies.Find(byAddress.Id).HousingBenefitClaimRef.Should().Be("HB000001");
                    db.Communications.Should().Contain(c => c.TemplateCode == "HbUnmatched");
                }
            }
        }

        [Fact]
        public void Compliance_reminders_raise_the_gas_service_and_do_not_repeat()
        {
            using (var h = new JobsHarness())
            {
                h.Run<ComplianceRemindersJob>(j => j.RunAsync());
                int reminders, services;
                using (var db = h.CreateDbContext())
                {
                    reminders = db.ComplianceReminders.Count();
                    services = db.WorkOrders.Count(w => w.Lines.Any(l => l.SorCode == "HT0301"));
                }
                reminders.Should().BeGreaterThan(0);
                services.Should().BeGreaterThan(0);

                h.Run<ComplianceRemindersJob>(j => j.RunAsync());
                using (var db = h.CreateDbContext())
                {
                    db.ComplianceReminders.Count().Should().Be(reminders);
                    db.WorkOrders.Count(w => w.Lines.Any(l => l.SorCode == "HT0301")).Should().Be(services);
                }
            }
        }

        [Fact]
        public void Sla_monitor_notifies_each_breach_once()
        {
            using (var h = new JobsHarness())
            {
                h.Run<RepairsSlaMonitorJob>(j => j.RunAsync());
                int notified, mails;
                using (var db = h.CreateDbContext())
                {
                    notified = db.WorkOrders.Count(w => w.SlaBreachNotified);
                    mails = db.Communications.Count(c => c.TemplateCode == "SlaBreach");
                }
                notified.Should().BeGreaterThan(0);
                mails.Should().BeGreaterThan(0);
                h.Run<RepairsSlaMonitorJob>(j => j.RunAsync());
                using (var db = h.CreateDbContext())
                    db.Communications.Count(c => c.TemplateCode == "SlaBreach").Should().Be(mails);
            }
        }

        [Fact]
        public void Geocoding_and_retention_run_against_the_simulators()
        {
            using (var h = new JobsHarness())
            {
                int missing;
                using (var db = h.CreateDbContext())
                {
                    missing = db.Properties.Count(p => p.Latitude == null);
                    db.Documents.Add(new Documents.DocumentRecord { DocumentKey = Guid.NewGuid(), Category = Documents.DocumentCategory.RepairPhoto, Title = "old photo", RetainUntil = JobsHarness.Today.AddDays(-1), UploadedAt = JobsHarness.Today.AddYears(-2) });
                    db.SaveChanges();
                }
                h.Run<GeocodePropertiesJob>(j => j.RunAsync());
                h.Run<DocumentRetentionJob>(j => j.RunAsync());
                using (var db = h.CreateDbContext())
                {
                    db.Properties.Count(p => p.Latitude == null).Should().BeLessThan(missing);
                    db.Documents.Single().Deleted.Should().BeTrue();
                }
                h.Documents.Deleted.Should().ContainSingle();
            }
        }
    }
}
