using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.XLANGs.BaseTypes;
using VanArsdel.Integration.Contracts.Canonical;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Rules;
using VanArsdel.Integration.Runtime.Configuration;
using VanArsdel.Integration.Runtime.Messages;
using VanArsdel.Integration.Runtime.Partners;
using VanArsdel.Integration.Runtime.Rules;

namespace VanArsdel.Integration.Runtime.Orchestration
{
    /// <summary>
    /// The expression shapes of ProcessCustomerOrder and SendCustomerDocument call these, so the orchestrations
    /// stay readable and the logic can be stepped through in a unit test.
    /// </summary>
    [Serializable]
    public static class OrderProcess
    {
        public static RuleOutcome Validate(XLANGMessage orderMessage)
        {
            var order = XLangMessages.ToOrder(orderMessage);
            var partner = PartnerCache.Get(order.Header.PartnerId);
            var now = DateTime.Now;
            var holidays = new MonitoringRepository(IntegrationDatabases.FromSso()).Holidays(now.Year);
            return PolicyExecutor.ValidateOrder(order, partner, now, holidays);
        }

        public static PartnerFacts PartnerFactsFor(string partnerId)
        {
            return new PartnerFacts(PartnerCache.Get(partnerId));
        }

        /// <summary>Runs the credit policy on the validation outcome, so ship-via and notes decided earlier are kept.</summary>
        public static RuleOutcome ReleaseCredit(XLANGMessage orderMessage, PartnerFacts partner, RuleOutcome outcome)
        {
            return PolicyExecutor.ReleaseCredit(XLangMessages.ToOrder(orderMessage), partner, outcome);
        }

        /// <summary>Applies ship-via, order type and priority decided by the policy to the order sent to the ERP.</summary>
        public static XmlDocument ApplyOutcome(XLANGMessage orderMessage, RuleOutcome outcome)
        {
            var order = XLangMessages.ToOrder(orderMessage);
            if (!string.IsNullOrEmpty(outcome.ShipVia))
            {
                order.Header.ShipVia = outcome.ShipVia;
            }

            if (!string.IsNullOrEmpty(outcome.OrderType))
            {
                order.Header.OrderType = outcome.OrderType;
            }

            if (outcome.Notes.Count > 0)
            {
                order.Header.Notes = string.Join(" / ", new[] { order.Header.Notes }.Concat(outcome.Notes).Where(n => !string.IsNullOrEmpty(n)));
            }

            return XLangMessages.FromCanonical(order);
        }

        public static decimal OrderValue(XLANGMessage orderMessage)
        {
            return new OrderFacts(XLangMessages.ToOrder(orderMessage)).GetOrderValue();
        }

        /// <summary>Mail body for the customer service team (SMTP send port, RawString part).</summary>
        public static string RejectionNotice(string partnerId, string orderNumber, RuleOutcome outcome)
        {
            var partner = PartnerCache.Directory.Find(partnerId);
            var sb = new StringBuilder();
            sb.AppendLine("Order " + orderNumber + " from " + (partner == null ? partnerId : partner.Name + " (" + partnerId + ")") + " was not passed to the ERP.");
            sb.AppendLine();
            foreach (var r in outcome.Rejections)
            {
                sb.AppendLine(" - " + r);
            }

            if (outcome.ManualReview)
            {
                sb.AppendLine(" - manual review requested");
            }

            sb.AppendLine();
            sb.AppendLine("Rules: " + string.Join(", ", outcome.FiredRules));
            sb.AppendLine("Resubmit from the operations portal after correcting the cross-reference or the order.");
            return sb.ToString();
        }

        public static string Subject(string prefix, string partnerId, string orderNumber)
        {
            return string.Format(CultureInfo.InvariantCulture, "[B2B] {0} {1} {2}", prefix, partnerId, orderNumber);
        }

        public static string RoutingSlipFor(string partnerId, string documentKind)
        {
            var partner = PartnerCache.Get(partnerId);
            return PolicyExecutor.ResolveRoutingSlip(partnerId, partner.Standard, partner.Transport, documentKind);
        }

        /// <summary>The partner endpoint of the slip's SEND step, for orchestrations that map with a fixed map.</summary>
        public static string EndpointFor(string partnerId, string documentKind)
        {
            var slip = VanArsdel.Integration.Domain.Routing.RoutingSlip.Parse(RoutingSlipFor(partnerId, documentKind));
            var send = slip.Steps.FirstOrDefault(s => s.Kind == VanArsdel.Integration.Domain.Routing.RoutingStepKind.Send);
            if (send == null)
            {
                throw new InvalidOperationException("The routing slip for " + partnerId + " " + documentKind + " has no SEND step.");
            }

            return send.Target;
        }

        public static string StandardOf(string partnerId)
        {
            return PartnerCache.Get(partnerId).Standard.ToString();
        }

        public static bool AckRequired(string partnerId)
        {
            return PartnerCache.Get(partnerId).AckRequired;
        }

        public static TimeSpan AckTimeout(string partnerId)
        {
            var p = PartnerCache.Get(partnerId);
            return TimeSpan.FromMinutes(p.AckTimeoutMinutes > 0 ? p.AckTimeoutMinutes : 1440);
        }

        public static string MailTo(string group)
        {
            return "mailto:" + new MonitoringRepository(IntegrationDatabases.FromSso()).NotifyGroups()[group];
        }
    }
}
