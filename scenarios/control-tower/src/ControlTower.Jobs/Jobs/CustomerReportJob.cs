using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Kpi;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Domain.Tracking;
using Proseware.ControlTower.Jobs.Infrastructure;

namespace Proseware.ControlTower.Jobs.Jobs;

/// <summary>
/// Monthly-to-date service report per customer: shipments, on-time performance and exceptions.
/// Written as .xlsx to the report folder and mailed to the customer's logistics contact.
/// </summary>
public sealed class CustomerReportJob
{
    private readonly ControlTowerDbContext _db;
    private readonly IClock _clock;
    private readonly Mailer _mailer;
    private readonly JobsOptions _options;
    private readonly ILogger<CustomerReportJob> _logger;

    public CustomerReportJob(ControlTowerDbContext db, IClock clock, Mailer mailer, IOptions<JobsOptions> options, ILogger<CustomerReportJob> logger)
    {
        _db = db;
        _clock = clock;
        _mailer = mailer;
        _options = options.Value;
        _logger = logger;
    }

    [DisableConcurrentExecution(3600)]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var customers = await _db.Customers.AsNoTracking().Where(c => c.ContactEmail != null && c.PortalEnabled).OrderBy(c => c.Code).ToListAsync(ct);
        foreach (var customer in customers)
        {
            try
            {
                await RunForCustomerAsync(customer.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One customer's failure must not stop the others; Hangfire retries the single customer job.
                _logger.LogError(ex, "Report for customer {Customer} failed, queued separately", customer.Code);
                BackgroundJob.Enqueue<CustomerReportJob>(j => j.RunForCustomerAsync(customer.Id, CancellationToken.None));
            }
        }
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunForCustomerAsync(int customerId, CancellationToken ct)
    {
        var customer = await _db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId, ct);
        var reportDate = _clock.UtcNow.UtcDateTime.Date.AddDays(-1);
        var from = new DateTime(reportDate.Year, reportDate.Month, 1);
        var fromUtc = new DateTimeOffset(from, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(reportDate.AddDays(1), TimeSpan.Zero);

        var shipments = await _db.Shipments.AsNoTracking()
            .Include(s => s.Origin).Include(s => s.Destination).Include(s => s.Packages).Include(s => s.Legs).ThenInclude(l => l.Carrier)
            .Where(s => s.CustomerId == customerId && s.PlannedPickup >= fromUtc && s.PlannedPickup < toUtc)
            .OrderBy(s => s.PlannedPickup)
            .AsSplitQuery()
            .ToListAsync(ct);
        var ids = shipments.Select(s => s.Id).ToList();
        var alerts = await _db.Alerts.AsNoTracking().Where(a => ids.Contains(a.ShipmentId)).ToListAsync(ct);
        var kpis = await _db.DailyKpis.AsNoTracking()
            .Where(k => k.CustomerId == customerId && k.Day >= from && k.Day <= reportDate)
            .ToListAsync(ct);

        var folder = Path.Combine(_options.ReportFolder, customer.Code);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{customer.Code}_service_report_{reportDate:yyyyMM}.xlsx");
        var tmp = file + ".tmp";
        using (var stream = File.Create(tmp))
        {
            var workbook = new CustomerReportWorkbook().Build(customer, from, reportDate, shipments, alerts, kpis);
            workbook.Write(stream);
        }
        File.Move(tmp, file, overwrite: true);
        _logger.LogInformation("Customer report {File} written: {Shipments} shipments", file, shipments.Count);

        if (_options.MailCustomerReports && !string.IsNullOrEmpty(customer.ContactEmail) && shipments.Count > 0)
        {
            await _mailer.SendAsync(customer.ContactEmail,
                $"Proseware Freight service report {from:MMMM yyyy} – {customer.Name}",
                $"Dear customer,\r\n\r\nattached is your service report for {from:MMMM yyyy} up to {reportDate:d.M.yyyy}.\r\n" +
                $"Shipments: {shipments.Count}, delivered: {shipments.Count(s => s.Status == ShipmentStatus.Delivered)}.\r\n\r\n" +
                "You can follow your shipments live at https://track.corp.local/.\r\n\r\nKind regards\r\nProseware Freight customer service",
                new[] { file }, ct);
        }
    }
}

/// <summary>Builds the customer service report workbook (NPOI).</summary>
public sealed class CustomerReportWorkbook
{
    private readonly KpiCalculator _kpi = new();

    public IWorkbook Build(Customer customer, DateTime from, DateTime to, IReadOnlyList<Shipment> shipments, IReadOnlyList<ShipmentAlert> alerts, IReadOnlyList<DailyKpi> kpis)
    {
        var wb = new XSSFWorkbook();
        var styles = new Styles(wb);

        BuildSummary(wb, styles, customer, from, to, shipments, alerts, kpis);
        BuildShipments(wb, styles, shipments);
        BuildExceptions(wb, styles, shipments, alerts);
        BuildDaily(wb, styles, kpis);
        return wb;
    }

    private void BuildSummary(XSSFWorkbook wb, Styles st, Customer customer, DateTime from, DateTime to, IReadOnlyList<Shipment> shipments, IReadOnlyList<ShipmentAlert> alerts, IReadOnlyList<DailyKpi> kpis)
    {
        var sheet = wb.CreateSheet("Summary");
        var r = 0;
        Cell(sheet, r++, 0, "Service report", st.Title);
        Cell(sheet, r++, 0, customer.Name, st.Bold);
        Cell(sheet, r++, 0, $"Period {from:dd.MM.yyyy} – {to:dd.MM.yyyy}");
        r++;

        var delivered = shipments.Where(s => s.Status == ShipmentStatus.Delivered).ToList();
        var onTime = delivered.Count(_kpi.IsDeliveredOnTime);
        var total = KpiCalculator.Sum(kpis);
        var rows = new (string, object, ICellStyle?)[]
        {
            ("Shipments", shipments.Count, null),
            ("Delivered", delivered.Count, null),
            ("Delivered on time", onTime, null),
            ("On-time delivery", delivered.Count == 0 ? 0d : (double)onTime / delivered.Count, st.Percent),
            ("On-time pickup", total.PickupsDue == 0 ? 0d : (double)total.PickedUpOnTime / total.PickupsDue, st.Percent),
            ("Average transit time (h)", Math.Round(total.AverageTransitHours, 1), st.Decimal),
            ("Gross weight (kg)", (double)shipments.Sum(s => s.GrossWeightKg), st.Decimal),
            ("Exceptions", alerts.Count, null),
            ("Critical exceptions", alerts.Count(a => a.Severity == Severity.Critical), null),
            ("Cancelled", shipments.Count(s => s.Status == ShipmentStatus.Cancelled), null)
        };
        foreach (var (label, value, style) in rows)
        {
            var row = sheet.CreateRow(r++);
            row.CreateCell(0).SetCellValue(label);
            var cell = row.CreateCell(1);
            if (value is int i) cell.SetCellValue(i); else cell.SetCellValue(Convert.ToDouble(value));
            if (style is not null) cell.CellStyle = style;
        }

        r++;
        Cell(sheet, r++, 0, "By transport mode", st.Bold);
        var header = sheet.CreateRow(r++);
        Header(header, st, "Mode", "Shipments", "Delivered", "On time", "On time %");
        foreach (var g in shipments.GroupBy(s => s.Mode).OrderBy(g => g.Key))
        {
            var d = g.Where(s => s.Status == ShipmentStatus.Delivered).ToList();
            var ot = d.Count(_kpi.IsDeliveredOnTime);
            var row = sheet.CreateRow(r++);
            row.CreateCell(0).SetCellValue(g.Key.ToString());
            row.CreateCell(1).SetCellValue(g.Count());
            row.CreateCell(2).SetCellValue(d.Count);
            row.CreateCell(3).SetCellValue(ot);
            var pct = row.CreateCell(4);
            pct.SetCellValue(d.Count == 0 ? 0 : (double)ot / d.Count);
            pct.CellStyle = st.Percent;
        }
        sheet.SetColumnWidth(0, 30 * 256);
        for (var c = 1; c <= 4; c++) sheet.SetColumnWidth(c, 14 * 256);
    }

    private void BuildShipments(XSSFWorkbook wb, Styles st, IReadOnlyList<Shipment> shipments)
    {
        var sheet = wb.CreateSheet("Shipments");
        var header = sheet.CreateRow(0);
        var columns = new[] { "Shipment", "Your reference", "Mode", "Status", "From", "To", "Carrier", "Packages", "Weight kg", "Planned pickup", "Picked up", "Planned delivery", "Delivered", "On time", "POD" };
        Header(header, st, columns);
        var r = 1;
        foreach (var s in shipments)
        {
            var row = sheet.CreateRow(r++);
            var c = 0;
            row.CreateCell(c++).SetCellValue(s.Number);
            row.CreateCell(c++).SetCellValue(s.CustomerReference ?? "");
            row.CreateCell(c++).SetCellValue(s.Mode.ToString());
            row.CreateCell(c++).SetCellValue(s.Status.ToString());
            row.CreateCell(c++).SetCellValue($"{s.Origin?.City}, {s.Origin?.CountryCode}");
            row.CreateCell(c++).SetCellValue($"{s.Destination?.City}, {s.Destination?.CountryCode}");
            row.CreateCell(c++).SetCellValue(string.Join(" / ", s.Legs.OrderBy(l => l.Sequence).Select(l => l.Carrier?.Name).Where(n => n is not null).Distinct()));
            row.CreateCell(c++).SetCellValue(s.PackageCount);
            var w = row.CreateCell(c++);
            w.SetCellValue((double)s.GrossWeightKg);
            w.CellStyle = st.Decimal;
            Date(row, c++, s.PlannedPickup, st);
            Date(row, c++, s.ActualPickup, st);
            Date(row, c++, s.PlannedDelivery, st);
            Date(row, c++, s.ActualDelivery, st);
            row.CreateCell(c++).SetCellValue(s.ActualDelivery is null ? "" : _kpi.IsDeliveredOnTime(s) ? "Yes" : "No");
            row.CreateCell(c).SetCellValue(s.PodSignedBy ?? "");
        }
        sheet.CreateFreezePane(1, 1);
        if (r > 1) sheet.SetAutoFilter(new CellRangeAddress(0, r - 1, 0, columns.Length - 1));
        for (var i = 0; i < columns.Length; i++) sheet.SetColumnWidth(i, (i is 4 or 5 or 6 ? 24 : 15) * 256);
    }

    private static void BuildExceptions(XSSFWorkbook wb, Styles st, IReadOnlyList<Shipment> shipments, IReadOnlyList<ShipmentAlert> alerts)
    {
        var sheet = wb.CreateSheet("Exceptions");
        Header(sheet.CreateRow(0), st, "Shipment", "Severity", "Type", "Description", "Raised", "Resolved", "Resolution");
        var numbers = shipments.ToDictionary(s => s.Id, s => s.Number);
        var r = 1;
        foreach (var a in alerts.OrderBy(a => a.RaisedAt))
        {
            var row = sheet.CreateRow(r++);
            row.CreateCell(0).SetCellValue(numbers.TryGetValue(a.ShipmentId, out var n) ? n : a.ShipmentId.ToString());
            row.CreateCell(1).SetCellValue(a.Severity.ToString());
            row.CreateCell(2).SetCellValue(a.RuleCode);
            row.CreateCell(3).SetCellValue(a.Message);
            Date(row, 4, a.RaisedAt, st);
            Date(row, 5, a.ResolvedAt, st);
            row.CreateCell(6).SetCellValue(a.Resolution ?? "");
        }
        sheet.SetColumnWidth(0, 15 * 256);
        sheet.SetColumnWidth(2, 18 * 256);
        sheet.SetColumnWidth(3, 70 * 256);
        sheet.SetColumnWidth(4, 17 * 256);
        sheet.SetColumnWidth(5, 17 * 256);
        sheet.SetColumnWidth(6, 40 * 256);
    }

    private static void BuildDaily(XSSFWorkbook wb, Styles st, IReadOnlyList<DailyKpi> kpis)
    {
        var sheet = wb.CreateSheet("Daily");
        Header(sheet.CreateRow(0), st, "Day", "Booked", "Delivered", "On time", "On time %", "Pickups due", "Picked up on time", "Exceptions");
        var r = 1;
        foreach (var g in kpis.GroupBy(k => k.Day).OrderBy(g => g.Key))
        {
            var sum = KpiCalculator.Sum(g);
            var row = sheet.CreateRow(r++);
            var day = row.CreateCell(0);
            day.SetCellValue(g.Key);
            day.CellStyle = st.Date;
            row.CreateCell(1).SetCellValue(sum.ShipmentsBooked);
            row.CreateCell(2).SetCellValue(sum.ShipmentsDelivered);
            row.CreateCell(3).SetCellValue(sum.DeliveredOnTime);
            var pct = row.CreateCell(4);
            pct.SetCellValue(sum.OnTimeDeliveryPercent / 100.0);
            pct.CellStyle = st.Percent;
            row.CreateCell(5).SetCellValue(sum.PickupsDue);
            row.CreateCell(6).SetCellValue(sum.PickedUpOnTime);
            row.CreateCell(7).SetCellValue(sum.AlertsRaised);
        }
        for (var i = 0; i < 8; i++) sheet.SetColumnWidth(i, 14 * 256);
    }

    private static void Cell(ISheet sheet, int row, int col, string text, ICellStyle? style = null)
    {
        var r = sheet.GetRow(row) ?? sheet.CreateRow(row);
        var c = r.CreateCell(col);
        c.SetCellValue(text);
        if (style is not null) c.CellStyle = style;
    }

    private static void Header(IRow row, Styles st, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            var c = row.CreateCell(i);
            c.SetCellValue(titles[i]);
            c.CellStyle = st.Header;
        }
    }

    private static void Date(IRow row, int col, DateTimeOffset? value, Styles st)
    {
        var c = row.CreateCell(col);
        if (value is null) return;
        // Customers read local Finnish time.
        c.SetCellValue(LocalTime.ToLocal(value.Value, "FLE Standard Time").DateTime);
        c.CellStyle = st.DateTime;
    }

    private sealed class Styles
    {
        public Styles(XSSFWorkbook wb)
        {
            var format = wb.CreateDataFormat();
            var bold = wb.CreateFont();
            bold.IsBold = true;
            var titleFont = wb.CreateFont();
            titleFont.IsBold = true;
            titleFont.FontHeightInPoints = 16;

            Title = wb.CreateCellStyle();
            Title.SetFont(titleFont);
            Bold = wb.CreateCellStyle();
            Bold.SetFont(bold);
            Header = wb.CreateCellStyle();
            Header.SetFont(bold);
            Header.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;
            Header.FillPattern = FillPattern.SolidForeground;
            Header.BorderBottom = BorderStyle.Thin;
            Percent = wb.CreateCellStyle();
            Percent.DataFormat = format.GetFormat("0.0%");
            Decimal = wb.CreateCellStyle();
            Decimal.DataFormat = format.GetFormat("#,##0.0");
            Date = wb.CreateCellStyle();
            Date.DataFormat = format.GetFormat("dd.mm.yyyy");
            DateTime = wb.CreateCellStyle();
            DateTime.DataFormat = format.GetFormat("dd.mm.yyyy hh:mm");
        }

        public ICellStyle Title { get; }
        public ICellStyle Bold { get; }
        public ICellStyle Header { get; }
        public ICellStyle Percent { get; }
        public ICellStyle Decimal { get; }
        public ICellStyle Date { get; }
        public ICellStyle DateTime { get; }
    }
}
