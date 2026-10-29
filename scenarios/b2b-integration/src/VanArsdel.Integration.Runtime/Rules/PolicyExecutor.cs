using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.RuleEngine;
using VanArsdel.Integration.Contracts.Canonical;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Rules;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.Runtime.Rules
{
    /// <summary>
    /// Runs the BRE policies from orchestration expression shapes with .NET facts (no Call Rules shape, so the
    /// facts can be built and inspected in C#). The rule engine caches the policy per host; we only dispose the
    /// Policy object after each execution.
    /// </summary>
    [Serializable]
    public static class PolicyExecutor
    {
        private static readonly TraceSource Trace = new TraceSource("VanArsdel.Integration.Rules");

        public static RuleOutcome ValidateOrder(Order order, TradingPartner partner, DateTime nowLocal, IEnumerable<DateTime> holidays)
        {
            var outcome = new RuleOutcome();
            var facts = new object[]
            {
                new OrderFacts(order),
                new PartnerFacts(partner),
                new CalendarFacts(nowLocal, SsoSettings.GetInt(SettingNames.OrderCutOffHour, 15), holidays),
                outcome
            };

            Execute(PolicyNames.OrderValidation, facts);
            Trace.TraceEvent(TraceEventType.Information, 1, "{0} {1}: {2}", partner.PartnerId, order.Header.OrderNumber, outcome.Summary());
            return outcome;
        }

        /// <summary>Second pass after the ERP credit check: release, hold or manual review.</summary>
        public static RuleOutcome ReleaseCredit(Order order, PartnerFacts partner, RuleOutcome outcome)
        {
            Execute(PolicyNames.CreditRelease, new object[] { new OrderFacts(order), partner, outcome });
            return outcome;
        }

        public static string ResolveRoutingSlip(string partnerId, DocumentStandard standard, TransportKind transport, string documentKind)
        {
            var facts = new RoutingFacts(partnerId, standard.ToString(), transport.ToString(), documentKind);
            Execute(PolicyNames.Routing, new object[] { facts });
            if (!facts.Resolved)
            {
                throw new InvalidOperationException("Policy " + PolicyNames.Routing + " has no route for " + partnerId + " " + documentKind + ".");
            }

            return facts.Slip;
        }

        private static void Execute(string policyName, object[] facts)
        {
            using (var policy = new Policy(policyName))
            {
                policy.Execute(facts);
            }
        }
    }
}
