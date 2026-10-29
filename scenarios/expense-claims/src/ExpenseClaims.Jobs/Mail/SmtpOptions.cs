namespace ExpenseClaims.Jobs.Mail;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.corp.local";
    public int Port { get; set; } = 25;
    public bool UseStartTls { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "Expense Claims <expenses-noreply@lamnahealthcare.com>";

    /// <summary>When set, mails are written as .eml files to this folder instead of being sent.</summary>
    public string? PickupFolder { get; set; }

    public string PortalUrl { get; set; } = "https://expenses.corp.local/";
}
