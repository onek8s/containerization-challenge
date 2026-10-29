using System;

namespace Woodgrove.Statements.Core.Model
{
    public static class DeliveryMethods
    {
        public const string Email = "EMAIL";
        public const string Share = "SHARE";
        /// <summary>Statement runs: each customer's own delivery setting (e-mail, drop folder or print pack).</summary>
        public const string Customer = "CUSTOMER";
        public const string Print = "PRINT";
    }

    public class Subscription
    {
        public int SubscriptionId { get; set; }
        public string Name { get; set; }
        public string ReportCode { get; set; }
        public string Schedule { get; set; }
        public string Parameters { get; set; }
        public string OutputFormat { get; set; }
        public string DeliveryMethod { get; set; }
        public string Recipients { get; set; }
        public string SharePath { get; set; }
        public string FileNameTemplate { get; set; }
        public string EmailSubject { get; set; }
        public string Owner { get; set; }
        public bool Enabled { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public string LastStatus { get; set; }
        public string ClaimedBy { get; set; }
        public DateTime? ClaimedUntil { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public class SubscriptionRun
    {
        public int RunId { get; set; }
        public int SubscriptionId { get; set; }
        public string SubscriptionName { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string Status { get; set; }
        public int Documents { get; set; }
        public int Failures { get; set; }
        public string Message { get; set; }
        public string RunBy { get; set; }
        public string Host { get; set; }
    }

    public static class RunStatus
    {
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        public const string Partial = "Partial";
        public const string Failed = "Failed";
    }

    public class DeliveryRecord
    {
        public long DeliveryId { get; set; }
        public int RunId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerNo { get; set; }
        public string CustomerName { get; set; }
        public string Method { get; set; }
        public string Target { get; set; }
        public string DocumentName { get; set; }
        public string ArchivePath { get; set; }
        public string Status { get; set; }
        public int Attempts { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string LastError { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string SubscriptionName { get; set; }
        public string EmailSubject { get; set; }
    }

    public class ApiClient
    {
        public int ClientId { get; set; }
        public string Name { get; set; }
        public string KeyPrefix { get; set; }
        public string KeyHash { get; set; }
        public string AllowedReports { get; set; }
        public bool Enabled { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        public bool Allows(string reportCode)
        {
            if (string.IsNullOrEmpty(AllowedReports)) return false;
            foreach (var code in AllowedReports.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (code == "*" || string.Equals(code, reportCode, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public class DocumentRequest
    {
        public string Channel { get; set; }
        public string RequestedBy { get; set; }
        public string ReportCode { get; set; }
        public string Parameters { get; set; }
        public string OutputFormat { get; set; }
        public int? Bytes { get; set; }
        public int? DurationMs { get; set; }
        public bool Succeeded { get; set; }
        public string Error { get; set; }
    }
}
