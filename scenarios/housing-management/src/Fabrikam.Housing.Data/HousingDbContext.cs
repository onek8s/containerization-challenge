using System;
using System.Linq;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Finance;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fabrikam.Housing.Data
{
    public class HousingDbContext : DbContext
    {
        public HousingDbContext(DbContextOptions<HousingDbContext> options) : base(options) { }

        public DbSet<Estate> Estates { get; set; }
        public DbSet<Patch> Patches { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<Tenancy> Tenancies { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<TenantEnquiry> Enquiries { get; set; }
        public DbSet<RentTransaction> RentTransactions { get; set; }
        public DbSet<DirectDebitMandate> Mandates { get; set; }
        public DbSet<DirectDebitPayment> DirectDebitPayments { get; set; }
        public DbSet<PaymentArrangement> Arrangements { get; set; }
        public DbSet<ArrearsCase> ArrearsCases { get; set; }
        public DbSet<ArrearsCaseNote> ArrearsCaseNotes { get; set; }
        public DbSet<ScheduleOfRatesItem> ScheduleOfRates { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkOrderLine> WorkOrderLines { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<WorkOrderEvent> WorkOrderEvents { get; set; }
        public DbSet<Contractor> Contractors { get; set; }
        public DbSet<ContractorInvoice> ContractorInvoices { get; set; }
        public DbSet<PaymentRun> PaymentRuns { get; set; }
        public DbSet<ComplianceCertificate> Certificates { get; set; }
        public DbSet<ComplianceReminder> ComplianceReminders { get; set; }
        public DbSet<DocumentRecord> Documents { get; set; }
        public DbSet<CommunicationLog> Communications { get; set; }
        public DbSet<JournalBatch> JournalBatches { get; set; }
        public DbSet<BankHoliday> BankHolidays { get; set; }
        public DbSet<Counter> Counters { get; set; }
        public DbSet<ProcessedWebhook> ProcessedWebhooks { get; set; }
        public DbSet<HousingBenefitImport> HousingBenefitImports { get; set; }

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.HasDefaultSchema(Database.IsSqlServer() ? "housing" : null);

            b.Entity<Estate>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.PatchCode).HasMaxLength(10);
            });
            b.Entity<Patch>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Name).HasMaxLength(100);
                e.Property(x => x.OfficerLogin).HasMaxLength(100);
                e.Property(x => x.OfficerName).HasMaxLength(100);
                e.Property(x => x.OfficerEmail).HasMaxLength(200);
            });
            b.Entity<Property>(e =>
            {
                e.Property(x => x.Reference).HasMaxLength(12).IsRequired();
                e.HasIndex(x => x.Reference).IsUnique();
                e.HasIndex(x => x.Uprn).IsUnique();
                e.Property(x => x.AddressLine1).HasMaxLength(100).IsRequired();
                e.Property(x => x.AddressLine2).HasMaxLength(100);
                e.Property(x => x.Town).HasMaxLength(60);
                e.Property(x => x.Postcode).HasMaxLength(8).IsRequired();
                e.HasIndex(x => x.Postcode);
                e.Property(x => x.PatchCode).HasMaxLength(10);
                e.Ignore(x => x.WeeklyTotalCharge);
                e.Ignore(x => x.SingleLineAddress);
                Money(e.Property(x => x.WeeklyNetRent));
                Money(e.Property(x => x.WeeklyServiceCharge));
                Money(e.Property(x => x.WeeklyWaterCharge));
            });
            b.Entity<Tenancy>(e =>
            {
                e.Property(x => x.Reference).HasMaxLength(12).IsRequired();
                e.HasIndex(x => x.Reference).IsUnique();
                e.Property(x => x.HousingBenefitClaimRef).HasMaxLength(20);
                Money(e.Property(x => x.WeeklyCharge));
                e.HasMany(x => x.Tenants).WithOne(x => x.Tenancy).HasForeignKey(x => x.TenancyId);
            });
            b.Entity<Tenant>(e =>
            {
                e.Property(x => x.Title).HasMaxLength(10);
                e.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
                e.Property(x => x.LastName).HasMaxLength(60).IsRequired();
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.MobilePhone).HasMaxLength(20);
                e.Property(x => x.PortalSubjectId).HasMaxLength(100);
                e.HasIndex(x => x.PortalSubjectId);
                e.Ignore(x => x.FullName);
            });
            b.Entity<TenantEnquiry>(e =>
            {
                e.Property(x => x.Subject).HasMaxLength(200).IsRequired();
                e.Property(x => x.Message).IsRequired();
                e.Ignore(x => x.TargetWorkingDays);
            });
            b.Entity<RentTransaction>(e =>
            {
                Money(e.Property(x => x.Amount));
                e.Property(x => x.Reference).HasMaxLength(40);
                e.Property(x => x.Description).HasMaxLength(200);
                e.Property(x => x.Source).HasMaxLength(30);
                e.HasIndex(x => new { x.TenancyId, x.RentWeek });
                e.HasIndex(x => x.JournalBatchId);
                e.Ignore(x => x.IsDebit);
            });
            b.Entity<DirectDebitMandate>(e =>
            {
                e.Property(x => x.ProviderCustomerId).HasMaxLength(40);
                e.Property(x => x.ProviderMandateId).HasMaxLength(40);
                e.HasIndex(x => x.ProviderMandateId);
                e.Property(x => x.BankReference).HasMaxLength(18);
                e.Property(x => x.AccountHolderName).HasMaxLength(100);
                e.Property(x => x.AccountNumberEnding).HasMaxLength(4);
                Money(e.Property(x => x.Amount));
            });
            b.Entity<DirectDebitPayment>(e =>
            {
                e.Property(x => x.ProviderPaymentId).HasMaxLength(40);
                e.HasIndex(x => x.ProviderPaymentId);
                Money(e.Property(x => x.Amount));
                e.Property(x => x.FailureReason).HasMaxLength(200);
            });
            b.Entity<PaymentArrangement>(e => Money(e.Property(x => x.WeeklyAmountTowardsArrears)));
            b.Entity<ArrearsCase>(e =>
            {
                Money(e.Property(x => x.BalanceAtStage));
                e.HasMany(x => x.Notes).WithOne().HasForeignKey(x => x.ArrearsCaseId);
            });
            b.Entity<ScheduleOfRatesItem>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Description).HasMaxLength(200);
                e.Property(x => x.Unit).HasMaxLength(10);
                Money(e.Property(x => x.UnitPrice));
            });
            b.Entity<WorkOrder>(e =>
            {
                e.Property(x => x.Reference).HasMaxLength(12).IsRequired();
                e.HasIndex(x => x.Reference).IsUnique();
                e.HasIndex(x => new { x.Status, x.ContractorId });
                e.Property(x => x.ReportedBy).HasMaxLength(100);
                e.Property(x => x.Location).HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(2000);
                e.Property(x => x.LabourHours).HasColumnType("decimal(6,2)");
                e.Ignore(x => x.IsOpen);
                e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.WorkOrderId);
                e.HasMany(x => x.Appointments).WithOne().HasForeignKey(x => x.WorkOrderId);
                e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.WorkOrderId);
            });
            b.Entity<WorkOrderLine>(e =>
            {
                e.Property(x => x.SorCode).HasMaxLength(10);
                e.Property(x => x.Description).HasMaxLength(200);
                e.Property(x => x.Quantity).HasColumnType("decimal(9,2)");
                Money(e.Property(x => x.UnitPrice));
                e.Ignore(x => x.LineTotal);
            });
            b.Entity<WorkOrderEvent>(e => e.Property(x => x.Actor).HasMaxLength(100));
            b.Entity<Contractor>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.ApiClientId).HasMaxLength(60);
                e.HasIndex(x => x.ApiClientId);
                e.Property(x => x.BankSortCode).HasMaxLength(8);
                e.Property(x => x.BankAccountNumber).HasMaxLength(8);
                e.Ignore(x => x.TradeList);
            });
            b.Entity<ContractorInvoice>(e =>
            {
                e.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
                e.HasIndex(x => new { x.ContractorId, x.InvoiceNumber }).IsUnique();
                Money(e.Property(x => x.NetAmount));
                Money(e.Property(x => x.VatAmount));
                e.Ignore(x => x.GrossAmount);
            });
            b.Entity<PaymentRun>(e => Money(e.Property(x => x.Total)));
            b.Entity<ComplianceCertificate>(e =>
            {
                e.Property(x => x.CertificateNumber).HasMaxLength(40);
                e.Property(x => x.EngineerName).HasMaxLength(100);
                e.Property(x => x.EngineerRegistration).HasMaxLength(20);
                e.HasIndex(x => new { x.PropertyId, x.Type });
            });
            b.Entity<DocumentRecord>(e =>
            {
                e.HasIndex(x => x.DocumentKey).IsUnique();
                e.Property(x => x.Title).HasMaxLength(200);
                e.Property(x => x.FileName).HasMaxLength(260);
                e.Property(x => x.ContentType).HasMaxLength(100);
                e.Property(x => x.Sha256).HasMaxLength(64);
                e.Property(x => x.StoragePath).HasMaxLength(400);
                e.Property(x => x.ThumbnailPath).HasMaxLength(400);
                e.HasIndex(x => x.TenancyId);
                e.HasIndex(x => x.PropertyId);
            });
            b.Entity<CommunicationLog>(e =>
            {
                e.Property(x => x.TemplateCode).HasMaxLength(40);
                e.Property(x => x.Recipient).HasMaxLength(200);
                e.Property(x => x.Subject).HasMaxLength(200);
                e.Property(x => x.ProviderReference).HasMaxLength(60);
            });
            b.Entity<JournalBatch>(e => Money(e.Property(x => x.TotalDebit)));
            b.Entity<BankHoliday>(e => e.HasKey(x => x.Date));
            b.Entity<Counter>(e => { e.HasKey(x => x.Name); e.Property(x => x.Name).HasMaxLength(40); });
            b.Entity<ProcessedWebhook>(e => { e.HasKey(x => x.EventId); e.Property(x => x.EventId).HasMaxLength(60); });
            b.Entity<HousingBenefitImport>(e => { e.Property(x => x.FileName).HasMaxLength(260); Money(e.Property(x => x.Total)); });
            b.Entity<AuditEntry>(e =>
            {
                e.ToTable("AuditEntries");
                e.Property(x => x.Actor).HasMaxLength(100);
                e.Property(x => x.Application).HasMaxLength(40);
                e.Property(x => x.EntityType).HasMaxLength(60);
                e.Property(x => x.EntityKey).HasMaxLength(60);
                e.Property(x => x.Action).HasMaxLength(60);
            });

            if (!Database.IsSqlServer())
            {
                // Development database: SQLite cannot sum decimals, so money is stored as REAL there.
                foreach (var entity in b.Model.GetEntityTypes())
                foreach (var p in entity.GetProperties())
                {
                    if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                        p.SetColumnType("REAL");
                    if (p.ClrType == typeof(decimal))
                        p.SetValueConverter(new ValueConverter<decimal, double>(v => (double)v, v => Math.Round((decimal)v, 2)));
                    else if (p.ClrType == typeof(decimal?))
                        p.SetValueConverter(new ValueConverter<decimal?, double?>(v => (double?)v, v => v.HasValue ? Math.Round((decimal)v.Value, 2) : (decimal?)null));
                }
            }
        }

        private static void Money(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal> p) => p.HasColumnType("decimal(12,2)");
    }

    public class Counter
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    public class ProcessedWebhook
    {
        public string EventId { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string ResourceType { get; set; }
        public string Action { get; set; }
    }

    public class HousingBenefitImport
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public DateTime ImportedAt { get; set; }
        public int Lines { get; set; }
        public int Matched { get; set; }
        public decimal Total { get; set; }
        public string Unmatched { get; set; }
    }
}
