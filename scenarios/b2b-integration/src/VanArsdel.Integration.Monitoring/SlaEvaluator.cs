using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VanArsdel.Integration.Domain.Sla;

namespace VanArsdel.Integration.Monitoring
{
    /// <summary>Applies the SLA definitions to a snapshot. Pure: no I/O, so the rules are unit tested.</summary>
    public class SlaEvaluator
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly BusinessCalendar _calendar;

        public SlaEvaluator(BusinessCalendar calendar)
        {
            _calendar = calendar ?? BusinessCalendar.Default;
        }

        public IList<Alert> Evaluate(MonitoringSnapshot snapshot, IEnumerable<SlaDefinition> definitions)
        {
            var alerts = new List<Alert>();
            foreach (var def in definitions.Where(d => d.Enabled))
            {
                switch (def.Kind)
                {
                    case SlaKind.AckOverdue:
                        alerts.AddRange(AckOverdue(snapshot, def));
                        break;
                    case SlaKind.OrderNotInErp:
                        alerts.AddRange(Orders(snapshot, def, o => !o.SentToErpUtc.HasValue && !o.RejectedUtc.HasValue && !(o.CreditHoldUtc.HasValue && !o.ReleasedUtc.HasValue),
                            o => o.ReceivedUtc, "not in the ERP"));
                        break;
                    case SlaKind.CreditHoldAging:
                        alerts.AddRange(Orders(snapshot, def, o => o.CreditHoldUtc.HasValue && !o.ReleasedUtc.HasValue && !o.RejectedUtc.HasValue,
                            o => o.CreditHoldUtc, "on credit hold"));
                        break;
                    case SlaKind.AsnNotSent:
                        alerts.AddRange(Orders(snapshot, def, o => o.DespatchedUtc.HasValue && !o.AsnSentUtc.HasValue, o => o.DespatchedUtc, "despatched without ASN"));
                        break;
                    case SlaKind.SuspendedInstances:
                        alerts.AddRange(Suspended(snapshot, def));
                        break;
                    case SlaKind.HostInstanceDown:
                        alerts.AddRange(snapshot.HostInstances
                            .Where(h => !h.Disabled && h.State != HostInstanceState.Running)
                            .Select(h => New(def, "HOST|" + h.Name, null, snapshot.TakenUtc,
                                "Host instance " + h.Name + " is " + h.State,
                                "BizTalk host instance " + h.HostName + " on " + h.ServerName + " reports " + h.State + ".")));
                        break;
                    case SlaKind.PartnerSilent:
                        alerts.AddRange(Silent(snapshot, def));
                        break;
                }
            }

            return alerts;
        }

        private IEnumerable<Alert> AckOverdue(MonitoringSnapshot snapshot, SlaDefinition def)
        {
            foreach (var i in snapshot.OutboundAwaitingAck.Where(x => def.AppliesTo(x.PartnerId) && x.AckStatus == Domain.Orders.AckStatus.Pending))
            {
                var due = i.AckDueUtc ?? Due(i.CreatedUtc, def);
                if (snapshot.TakenUtc <= due)
                {
                    continue;
                }

                yield return New(def, "ACK|" + i.PartnerId + "|" + i.ControlNumber, i.PartnerId, snapshot.TakenUtc,
                    "No acknowledgement from " + i.PartnerId + " for interchange " + i.ControlNumber,
                    i.TransactionType + " interchange " + i.ControlNumber + " (" + i.TransactionCount + " sets) sent "
                    + i.CreatedUtc.ToString("yyyy-MM-dd HH:mm", Inv) + " UTC, acknowledgement was due " + due.ToString("yyyy-MM-dd HH:mm", Inv) + " UTC.");
            }
        }

        private IEnumerable<Alert> Orders(MonitoringSnapshot snapshot, SlaDefinition def, Func<OrderActivityRow, bool> open, Func<OrderActivityRow, DateTime?> since, string what)
        {
            foreach (var o in snapshot.OpenOrders.Where(x => def.AppliesTo(x.PartnerId) && open(x)))
            {
                var start = since(o);
                if (!start.HasValue)
                {
                    continue;
                }

                var age = def.BusinessHoursOnly ? _calendar.BusinessTimeBetween(start.Value, snapshot.TakenUtc) : snapshot.TakenUtc - start.Value;
                if (age < def.Threshold)
                {
                    continue;
                }

                yield return New(def, def.Code + "|" + o.PartnerId + "|" + o.OrderNumber, o.PartnerId, snapshot.TakenUtc,
                    "Order " + o.OrderNumber + " from " + o.PartnerId + " " + what + " for " + Format(age),
                    "Order " + o.OrderNumber + " (" + o.LineCount + " lines, value " + o.OrderValue.ToString("0.00", Inv) + ") " + what
                    + " since " + start.Value.ToString("yyyy-MM-dd HH:mm", Inv) + " UTC" + (string.IsNullOrEmpty(o.HoldReason) ? "." : ": " + o.HoldReason + "."));
            }
        }

        private IEnumerable<Alert> Suspended(MonitoringSnapshot snapshot, SlaDefinition def)
        {
            foreach (var g in snapshot.Suspended)
            {
                var age = snapshot.TakenUtc - g.OldestSuspendedUtc;
                var countBreached = def.ThresholdCount > 0 && g.Count >= def.ThresholdCount;
                var ageBreached = def.ThresholdMinutes > 0 && age >= def.Threshold;
                if (!countBreached && !ageBreached)
                {
                    continue;
                }

                yield return New(def, "SUSP|" + g.ServiceName, null, snapshot.TakenUtc,
                    g.Count + " suspended instance(s) of " + Short(g.ServiceName),
                    g.Count + " suspended " + (g.Resumable ? "(resumable)" : "(not resumable)") + " instance(s) of " + g.ServiceName + " on " + g.HostName
                    + ", oldest " + Format(age) + ". Last error: " + (g.ErrorSample ?? "n/a"));
            }
        }

        private IEnumerable<Alert> Silent(MonitoringSnapshot snapshot, SlaDefinition def)
        {
            foreach (var p in snapshot.Partners.Where(x => x.ExpectsDailyTraffic && def.AppliesTo(x.PartnerId)))
            {
                if (!p.LastReceivedUtc.HasValue)
                {
                    continue;
                }

                var quiet = def.BusinessHoursOnly ? _calendar.BusinessTimeBetween(p.LastReceivedUtc.Value, snapshot.TakenUtc) : snapshot.TakenUtc - p.LastReceivedUtc.Value;
                if (quiet < def.Threshold)
                {
                    continue;
                }

                yield return New(def, "SILENT|" + p.PartnerId, p.PartnerId, snapshot.TakenUtc,
                    "Nothing received from " + p.PartnerId + " for " + Format(quiet),
                    "Last document from " + p.PartnerId + " arrived " + p.LastReceivedUtc.Value.ToString("yyyy-MM-dd HH:mm", Inv) + " UTC. Check the partner's AS2/SFTP connection.");
            }
        }

        private DateTime Due(DateTime fromUtc, SlaDefinition def)
        {
            return def.BusinessHoursOnly ? _calendar.AddBusinessTime(fromUtc, def.Threshold) : fromUtc + def.Threshold;
        }

        private static Alert New(SlaDefinition def, string key, string partnerId, DateTime nowUtc, string subject, string detail)
        {
            return new Alert
            {
                Key = key,
                SlaCode = def.Code,
                Kind = def.Kind,
                Severity = def.Severity,
                PartnerId = partnerId,
                Subject = subject,
                Detail = detail,
                RaisedUtc = nowUtc,
                NotifyGroup = def.NotifyGroup
            };
        }

        private static string Short(string serviceName)
        {
            var i = serviceName.LastIndexOf('.');
            return i < 0 ? serviceName : serviceName.Substring(i + 1);
        }

        public static string Format(TimeSpan span)
        {
            if (span.TotalDays >= 1)
            {
                return ((int)span.TotalDays).ToString(Inv) + "d " + span.Hours.ToString(Inv) + "h";
            }

            if (span.TotalHours >= 1)
            {
                return ((int)span.TotalHours).ToString(Inv) + "h " + span.Minutes.ToString(Inv) + "m";
            }

            return ((int)span.TotalMinutes).ToString(Inv) + "m";
        }
    }
}
