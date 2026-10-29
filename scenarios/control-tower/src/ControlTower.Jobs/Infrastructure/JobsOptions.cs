namespace Proseware.ControlTower.Jobs.Infrastructure;

public sealed class JobsOptions
{
    public const string Section = "Jobs";

    /// <summary>Hangfire storage: SqlServer (default) or Memory.</summary>
    public string Storage { get; set; } = "SqlServer";

    public int WorkerCount { get; set; } = 4;

    /// <summary>Time zone used for the cron schedules (Windows id).</summary>
    public string ScheduleTimeZone { get; set; } = "FLE Standard Time";

    public string KpiCron { get; set; } = "*/15 * * * *";
    public string CustomerReportCron { get; set; } = "30 5 * * *";
    public string InvoiceExportCron { get; set; } = "0 22 * * 1-5";
    public string DocumentPurgeCron { get; set; } = "15 3 * * *";
    public string PositionRetentionCron { get; set; } = "45 3 * * *";
    public string FuelSurchargeCron { get; set; } = "*/30 * * * *";

    public string ReportFolder { get; set; } = @"D:\ControlTower\Reports";
    public string InvoiceExportFolder { get; set; } = @"D:\ControlTower\Export\Invoices";
    public string FuelImportFolder { get; set; } = @"D:\ControlTower\Import\Fuel";

    /// <summary>ERP company code written into every invoice line.</summary>
    public string ErpCompany { get; set; } = "PF01";

    /// <summary>Prefix of invoice numbers created by the export, followed by yyMMdd and a running number.</summary>
    public string InvoicePrefix { get; set; } = "70";

    public int PositionRetentionDays { get; set; } = 90;

    public bool MailCustomerReports { get; set; } = true;

    public DashboardOptions Dashboard { get; set; } = new();
}

public sealed class DashboardOptions
{
    /// <summary>Remote users must sign in with these basic credentials; local requests are always allowed.</summary>
    public string User { get; set; } = "ops";
    public string Password { get; set; } = "";
}

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "Proseware Freight <no-reply@proseware-freight.example>";
    public string? BlindCopy { get; set; }
}
