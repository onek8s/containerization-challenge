using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Statements;

namespace Woodgrove.Statements.Web.Models
{
    public class DashboardModel
    {
        public LedgerLoad LastLoad { get; set; }
        public IList<SubscriptionRun> RecentRuns { get; set; }
        public IList<DeliveryRecord> Problems { get; set; }
        public IList<Subscription> Upcoming { get; set; }
    }

    public class CustomerListModel
    {
        public string Query { get; set; }
        public string CreditController { get; set; }
        public IList<string> CreditControllers { get; set; }
        public IList<CustomerBalance> Customers { get; set; }
    }

    public class CustomerDetailsModel
    {
        public Customer Customer { get; set; }
        public Statement Current { get; set; }
        public IList<LeaseContract> Contracts { get; set; }
        public IList<DeliveryRecord> Deliveries { get; set; }
        public DateTime DefaultPeriodStart { get; set; }
        public DateTime DefaultStatementDate { get; set; }
        public bool CanEditDelivery { get; set; }
    }

    public class DeliverySettingsModel
    {
        [Required]
        public string CustomerNo { get; set; }
        public StatementDelivery Delivery { get; set; }
        [EmailAddress, StringLength(200)]
        public string Email { get; set; }
        [StringLength(260)]
        public string DropFolder { get; set; }
    }

    public class ReportCatalogModel
    {
        public IList<ReportDefinitionInfo> Reports { get; set; }
        public IList<string> CreditControllers { get; set; }
        public DateTime DefaultAsOf { get; set; }
    }

    public class SubscriptionEditModel
    {
        public int SubscriptionId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, Display(Name = "Report")]
        public string ReportCode { get; set; }

        [Required, StringLength(40)]
        public string Schedule { get; set; }

        [StringLength(1000)]
        public string Parameters { get; set; }

        [Required, Display(Name = "Format")]
        public string OutputFormat { get; set; }

        [Required, Display(Name = "Delivery")]
        public string DeliveryMethod { get; set; }

        [StringLength(1000)]
        public string Recipients { get; set; }

        [StringLength(260), Display(Name = "Share path")]
        public string SharePath { get; set; }

        [StringLength(120), Display(Name = "File name")]
        public string FileNameTemplate { get; set; }

        [StringLength(200), Display(Name = "E-mail subject")]
        public string EmailSubject { get; set; }

        public bool Enabled { get; set; } = true;

        public string Owner { get; set; }
        public IList<DateTime> NextRuns { get; set; } = new List<DateTime>();
        public IList<ReportDefinitionInfo> Reports { get; set; }
    }

    public class RunDetailsModel
    {
        public SubscriptionRun Run { get; set; }
        public IList<DeliveryRecord> Deliveries { get; set; }
    }

    public class ApiClientCreateModel
    {
        [Required, StringLength(60)]
        public string Name { get; set; }

        [Required, StringLength(20), RegularExpression("^[a-z0-9]{2,20}$", ErrorMessage = "Use 2-20 lower case letters or digits.")]
        public string Prefix { get; set; }

        [Required, StringLength(200), Display(Name = "Allowed reports")]
        public string AllowedReports { get; set; }
    }

    public class ApiKeyCreatedModel
    {
        public string Name { get; set; }
        public string Key { get; set; }
    }
}
