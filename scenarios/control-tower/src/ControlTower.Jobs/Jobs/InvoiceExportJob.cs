using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Jobs.Infrastructure;

namespace Proseware.ControlTower.Jobs.Jobs;

/// <summary>
/// Hands delivered, uninvoiced charges to the ERP: one invoice per customer and run,
/// written as a semicolon separated file that the ERP import agent picks up from the export folder.
/// </summary>
public sealed class InvoiceExportJob
{
    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;
    private readonly JobsOptions _options;
    private readonly ILogger<InvoiceExportJob> _logger;

    public InvoiceExportJob(ControlTowerDbContext db, IClock clock, IOptions<JobsOptions> options, ILogger<InvoiceExportJob> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    [DisableConcurrentExecution(1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var shipments = await _db.Shipments
            .Include(s => s.Customer)
            .Include(s => s.Charges)
            .Where(s => s.Status == ShipmentStatus.Delivered && s.Charges.Any(c => !c.Invoiced))
            .OrderBy(s => s.CustomerId).ThenBy(s => s.Number)
            .AsSplitQuery()
            .ToListAsync(ct);
        if (shipments.Count == 0)
        {
            _logger.LogInformation("No delivered shipments to invoice");
            return;
        }

        Directory.CreateDirectory(_options.InvoiceExportFolder);
        var runNumber = Directory.GetFiles(_options.InvoiceExportFolder, $"INV_{now:yyyyMMdd}_*.csv").Length + 1;
        var file = Path.Combine(_options.InvoiceExportFolder, $"INV_{now:yyyyMMdd}_{runNumber:D2}.csv");
        var tmp = file + ".part";

        var lines = new List<InvoiceLine>();
        var invoiceSeq = 0;
        foreach (var customerGroup in shipments.GroupBy(s => s.Customer!))
        {
            var invoiceNumber = $"{_options.InvoicePrefix}{now:yyMMdd}{runNumber:D2}{++invoiceSeq:D3}";
            var lineNo = 0;
            foreach (var shipment in customerGroup)
            {
                foreach (var charge in shipment.Charges.Where(c => !c.Invoiced).OrderBy(c => c.Id))
                {
                    lines.Add(new InvoiceLine
                    {
                        Company = _options.ErpCompany,
                        InvoiceNumber = invoiceNumber,
                        LineNumber = ++lineNo,
                        CustomerCode = customerGroup.Key.Code,
                        CustomerVat = customerGroup.Key.VatNumber ?? "",
                        InvoiceDate = now.UtcDateTime.Date,
                        Currency = charge.Currency,
                        Shipment = shipment.Number,
                        CustomerReference = shipment.CustomerReference ?? "",
                        ChargeCode = charge.Code,
                        Description = charge.Description,
                        Amount = charge.Amount,
                        VatCode = VatCode(customerGroup.Key.CountryCode, shipment),
                        DeliveredOn = shipment.ActualDelivery?.UtcDateTime.Date
                    });
                    charge.Invoiced = true;
                    charge.InvoiceNumber = invoiceNumber;
                }
            }
        }

        // Write first, then mark the charges; a failed write leaves the charges for the next run.
        await using (var writer = new StreamWriter(tmp, false, Encoding.GetEncoding(1252)))
        await using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.GetCultureInfo("fi-FI")) { Delimiter = ";", HasHeaderRecord = true }))
        {
            csv.Context.RegisterClassMap<InvoiceLineMap>();
            await csv.WriteRecordsAsync(lines, ct);
        }

        await _db.SaveChangesAsync(ct);
        File.Move(tmp, file, overwrite: false);
        _logger.LogInformation("Invoice export {File}: {Invoices} invoices, {Lines} lines, {Total} EUR",
            file, invoiceSeq, lines.Count, lines.Where(l => l.Currency == "EUR").Sum(l => l.Amount));
    }

    /// <summary>ERP VAT codes: domestic 24 %, EU reverse charge, export 0 %.</summary>
    internal static string VatCode(string customerCountry, Shipment s)
    {
        if (s.CrossesCustomsBorder) return "V0X";
        if (customerCountry == "FI") return "V24";
        return "VRC";
    }

    public sealed class InvoiceLine
    {
        public string Company { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public int LineNumber { get; set; }
        public string CustomerCode { get; set; } = "";
        public string CustomerVat { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public string Currency { get; set; } = "";
        public string Shipment { get; set; } = "";
        public string CustomerReference { get; set; } = "";
        public string ChargeCode { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string VatCode { get; set; } = "";
        public DateTime? DeliveredOn { get; set; }
    }

    private sealed class InvoiceLineMap : ClassMap<InvoiceLine>
    {
        public InvoiceLineMap()
        {
            Map(l => l.Company).Name("YRITYS");
            Map(l => l.InvoiceNumber).Name("LASKUNRO");
            Map(l => l.LineNumber).Name("RIVI");
            Map(l => l.CustomerCode).Name("ASIAKAS");
            Map(l => l.CustomerVat).Name("YTUNNUS");
            Map(l => l.InvoiceDate).Name("LASKUPVM").TypeConverterOption.Format("dd.MM.yyyy");
            Map(l => l.Currency).Name("VALUUTTA");
            Map(l => l.Shipment).Name("LAHETYS");
            Map(l => l.CustomerReference).Name("VIITE");
            Map(l => l.ChargeCode).Name("NIMIKE");
            Map(l => l.Description).Name("SELITE");
            Map(l => l.Amount).Name("SUMMA").TypeConverterOption.Format("0.00");
            Map(l => l.VatCode).Name("ALVKOODI");
            Map(l => l.DeliveredOn).Name("TOIMPVM").TypeConverterOption.Format("dd.MM.yyyy");
        }
    }
}
