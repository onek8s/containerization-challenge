using System;
using System.Configuration;
using System.Globalization;
using CreditDesk.Core;

namespace CreditDesk.CreditService
{
    internal static class CreditServiceSettings
    {
        public static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["CreditDesk"].ConnectionString;

        /// <summary>Format name of the transactional letter queue on the document server.</summary>
        public static string LetterQueue => Get("LetterQueue", @"FormatName:DIRECT=OS:docs01.fabrikam.local\private$\creditdesk.letters");

        public static string SalesRepGroup => Get("Group.SalesRep", @"FABRIKAM\CreditDesk Sales");
        public static string CreditControllerGroup => Get("Group.CreditController", @"FABRIKAM\CreditDesk Controllers");
        public static string FinanceManagerGroup => Get("Group.FinanceManager", @"FABRIKAM\CreditDesk Finance Managers");
        public static string CreditCommitteeGroup => Get("Group.CreditCommittee", @"FABRIKAM\CreditDesk Committee");

        public static ApprovalPolicy Policy => new ApprovalPolicy
        {
            AutomaticIncreaseLimit = GetDecimal("Policy.AutomaticIncreaseLimit", 2500m),
            ControllerLimit = GetDecimal("Policy.ControllerLimit", 50000m),
            FinanceManagerLimit = GetDecimal("Policy.FinanceManagerLimit", 250000m),
            MaximumMultiplier = GetDecimal("Policy.MaximumMultiplier", 3m),
            MaxOverdueDays = (int)GetDecimal("Policy.MaxOverdueDays", 60m),
            MinimumCustomerMonths = (int)GetDecimal("Policy.MinimumCustomerMonths", 6m),
            ApprovalValidityDays = (int)GetDecimal("Policy.ApprovalValidityDays", 365m)
        };

        private static string Get(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static decimal GetDecimal(string key, decimal fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        }
    }
}
