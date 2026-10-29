namespace FurnaceRecords.Worker;

public sealed class WorkerOptions
{
    public const string Section = "Worker";

    /// <summary>MES event endpoint; events are posted to <c>{MesAddress}/events/{topic}</c>.</summary>
    public string MesAddress { get; set; } = "https://mes.corp.local/api/heat-treat/";

    public int OutboxBatchSize { get; set; } = 20;

    /// <summary>Folder for released certificates and survey reports, one sub-folder per year.</summary>
    public string CertificateFolder { get; set; } = @"\\files01.corp.local\FurnaceRecords$\Certificates";

    public string SmtpHost { get; set; } = "smtp.corp.local";
    public int SmtpPort { get; set; } = 25;

    /// <summary>When set, mails are written as .eml files to this folder instead of being sent.</summary>
    public string? MailPickupFolder { get; set; }

    public string MailFrom { get; set; } = "furnace-records@corp.local";
    public List<string> ReminderRecipients { get; set; } = new() { "quality-heat-treat@corp.local" };

    /// <summary>Local time of the daily calibration reminder.</summary>
    public TimeOnly ReminderTime { get; set; } = new(6, 0);

    public int ReminderDays { get; set; } = 14;

    public string TimeZone { get; set; } = "W. Europe Standard Time";
}
