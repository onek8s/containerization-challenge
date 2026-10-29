using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lucerne.ProjectSites.Data.Entities
{
    [Table("KpiSnapshot", Schema = "dash")]
    public class KpiSnapshot
    {
        public long Id { get; set; }

        [Required, StringLength(10)]
        public string ProjectNumber { get; set; }

        [Column(TypeName = "date")]
        public DateTime SnapshotDate { get; set; }

        [StringLength(10)] public string OfficeCode { get; set; }
        [StringLength(12)] public string Overall { get; set; }
        [StringLength(12)] public string Schedule { get; set; }
        [StringLength(12)] public string Budget { get; set; }
        [StringLength(12)] public string Risk { get; set; }
        public decimal HoursToDate { get; set; }
        public decimal BudgetHours { get; set; }
        public decimal BurnRatio { get; set; }
        public decimal PercentComplete { get; set; }
        public int DeliverablesTotal { get; set; }
        public int DeliverablesIssued { get; set; }
        public int DeliverablesOverdue { get; set; }
        public int OpenRfis { get; set; }
        public int OpenHighRisks { get; set; }
    }

    [Table("Subscription", Schema = "dash")]
    public class ProjectSubscription
    {
        public int Id { get; set; }

        [Required, StringLength(256)]
        public string UserLogin { get; set; }

        [Required, StringLength(10)]
        public string ProjectNumber { get; set; }

        [StringLength(256)]
        public string Email { get; set; }

        /// <summary>Weekly or Monthly status digest.</summary>
        [StringLength(10)]
        public string Frequency { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? LastSentUtc { get; set; }
    }

    [Table("UserPreference", Schema = "dash")]
    public class UserPreference
    {
        [Key, StringLength(256)]
        public string UserLogin { get; set; }

        [StringLength(10)] public string DefaultOffice { get; set; }
        [StringLength(10)] public string DefaultView { get; set; }
        public bool ShowClosed { get; set; }
    }

    [Table("Watermark", Schema = "sync")]
    public class SyncWatermark
    {
        [Key, StringLength(64)]
        public string Name { get; set; }

        public DateTime ValueUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    [Table("Run", Schema = "sync")]
    public class SyncRunRecord
    {
        public long Id { get; set; }

        [Required, StringLength(64)]
        public string Job { get; set; }

        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public bool Succeeded { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }

        [StringLength(2000)]
        public string Message { get; set; }

        [StringLength(64)]
        public string MachineName { get; set; }
    }
}
