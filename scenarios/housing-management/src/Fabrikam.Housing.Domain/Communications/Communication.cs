using System;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Communications
{
    public enum Channel
    {
        Sms = 1,
        Email = 2,
        Letter = 3
    }

    public enum CommunicationStatus
    {
        Queued = 1,
        Sent = 2,
        Delivered = 3,
        Failed = 4,
        Printed = 5
    }

    public class CommunicationLog : Entity
    {
        public int? TenancyId { get; set; }
        public int? ContractorId { get; set; }
        public Channel Channel { get; set; }
        public string TemplateCode { get; set; }
        public string Recipient { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public CommunicationStatus Status { get; set; }
        public string ProviderReference { get; set; }
        public string Error { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int? DocumentId { get; set; }
    }

    /// <summary>
    /// SMS texts. Messages must fit in two segments (306 GSM characters).
    /// </summary>
    public static class SmsTemplates
    {
        public const int MaxLength = 306;

        public static string ArrearsReminder(string firstName, decimal balance) =>
            $"Hi {firstName}, your Fabrikam Homes rent account is {balance:N2} GBP in arrears. Please pay at tenants.fabrikamhomes.co.uk or call 0300 123 4567 if you need help. Reply STOP to opt out.";

        public static string RepairAppointment(string reference, DateTime date, string slot) =>
            $"Fabrikam Homes: your repair {reference} is booked for {date:ddd d MMM} ({slot}). To change it visit tenants.fabrikamhomes.co.uk or call 0300 123 4567.";

        public static string GasServiceReminder(string firstName, DateTime due) =>
            $"Hi {firstName}, your annual gas safety check is due by {due:d MMM yyyy}. It is a legal requirement. Book a time at tenants.fabrikamhomes.co.uk or call 0300 123 4567.";

        public static string DirectDebitFailed(string firstName, decimal amount) =>
            $"Hi {firstName}, your direct debit of {amount:N2} GBP to Fabrikam Homes was not paid by your bank. Please pay online or call 0300 123 4567.";

        public static string Truncate(string text) => text.Length <= MaxLength ? text : text.Substring(0, MaxLength - 3) + "...";
    }
}
