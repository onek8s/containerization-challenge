using System;
using Coho.Billing.Core.Runs;

namespace Coho.Billing.Data
{
    public class BillCycle
    {
        public string CycleCode { get; set; }
        public string Name { get; set; }
        public int IntervalMonths { get; set; }
        public DateTime NextCutOff { get; set; }
        public int ReadWindowDays { get; set; }
        public int PaymentDays { get; set; }
    }

    public class StoredRead : Core.MeterRead
    {
        public int ReadId { get; set; }
        public int Status { get; set; }
    }

    public class BillException
    {
        public int ExceptionId { get; set; }
        public int RunId { get; set; }
        public string AccountNumber { get; set; }
        public string CustomerName { get; set; }
        public int? ReadId { get; set; }
        public string Code { get; set; }
        public decimal? Consumption { get; set; }
        public decimal? Expected { get; set; }
        public ExceptionResolution Resolution { get; set; }
        public string ResolvedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string Note { get; set; }
    }

    public class RunEvent
    {
        public DateTime At { get; set; }
        public string Level { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }
    }

    public class ImportFileRecord
    {
        public int ImportFileId { get; set; }
        public string FileName { get; set; }
        public int Source { get; set; }
        public string BatchId { get; set; }
        public DateTime ReceivedAt { get; set; }
        public int ReadCount { get; set; }
        public int RejectedCount { get; set; }
        public int Status { get; set; }
        public string Message { get; set; }
    }

    public class ReprintRequest
    {
        public int RequestId { get; set; }
        public string BillNumber { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Reason { get; set; }
    }

    public static class BillStatus
    {
        public const int Draft = 0;
        public const int Issued = 1;
        public const int Excluded = 2;
    }

    public static class ReadStatus
    {
        public const int New = 0;
        public const int Billed = 1;
        public const int Discarded = 2;
        public const int Superseded = 3;
    }
}
