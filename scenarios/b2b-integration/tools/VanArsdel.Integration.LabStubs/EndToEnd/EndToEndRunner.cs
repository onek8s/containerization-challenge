using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Domain.Sla;
using VanArsdel.Integration.LabStubs.Lab;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.LabStubs.EndToEnd
{
    /// <summary>
    /// Two business days of traffic through the lab: customer orders over AS2, SFTP, FTP and the gateway, ERP
    /// booking, purchase orders to suppliers, goods issue and billing back to the customers, acknowledgements both
    /// ways, supplier invoices to the ERP, the ack timeout and an SLA evaluation. Checks the outcome at the end.
    /// </summary>
    public class EndToEndRunner
    {
        private readonly List<(string Name, bool Passed, string Detail)> _checks = new List<(string, bool, string)>();
        private LabHost _host;
        private LabContext _lab;

        public async Task<int> RunAsync(string workFolder)
        {
            Directory.CreateDirectory(workFolder);
            Console.WriteLine("work folder: " + workFolder);
            _lab = new LabContext(workFolder, Seed.SeedData.Load(ScenarioPaths.Seed), new DateTime(2026, 3, 2, 8, 20, 0));
            await using (_host = new LabHost(_lab))
            {
                await _host.StartAsync();
                Console.WriteLine("lab endpoints on " + _host.BaseUrl + " (ERP service " + LabHost.ErpServicePath + ")");
                _host.Partners.WithoutAcks.Add("S5001");

                Step("08:20 customer orders over AS2", 8, 20, () =>
                {
                    Submit("C1001", "C1001_850_HW-55012.x12");
                    Submit("C2001", "C2001_ORDERS_BW-778812.edi");
                });

                Step("08:35 SFTP and FTP pickups", 8, 35, () =>
                {
                    Submit("C1002", "C1002_850_BF-2026-0311.x12");
                    Submit("C1002", "C1002_850_BF-2026-0312.x12");
                    Submit("C2002", "C2002_ORDERS_NB-26-0412.edi");
                    _host.Platform.PollFolders();
                });

                Step("08:50 gateway batches", 8, 50, () =>
                {
                    Gateway("C3001_OrderBatch_RW-20260302-01.xml");
                    Gateway("C3002_OrderBatch_LE-20260302.xml");
                });

                Step("09:05 Hartwell sends HW-55012 again", 9, 5, () => Submit("C1001", "C1001_850_HW-55012.x12"));

                Step("09:15 partners collect, ERP books orders", 9, 15, () =>
                {
                    _host.Partners.CollectFolders();
                    _host.Erp.ProcessInbound();
                });

                Step("10:00 ERP releases purchase orders", 10, 0, () =>
                {
                    _host.Erp.ReleasePurchaseOrders();
                    _host.Platform.PollFolders();
                    _host.Partners.CollectFolders();
                    _host.Partners.SendAcknowledgements();
                    _host.Platform.PollFolders();
                });

                Step("14:00 goods issue", 14, 0, () =>
                {
                    _host.Erp.GoodsIssue();
                    _host.Platform.PollFolders();
                    _host.Partners.CollectFolders();
                });

                Step("14:30 partners acknowledge the ASNs", 14, 30, () =>
                {
                    _host.Partners.SendAcknowledgements();
                    _host.Platform.PollFolders();
                });

                Step("18:00 billing run", 18, 0, () =>
                {
                    _host.Erp.Billing();
                    _host.Platform.PollFolders();
                    _host.Partners.CollectFolders();
                    _host.Partners.SendAcknowledgements();
                    _host.Platform.PollFolders();
                });

                _lab.Now = new DateTime(2026, 3, 3, 11, 5, 0);
                Step("next day 11:05 supplier invoices", 11, 5, () =>
                {
                    Submit("S5002", "S5002_810_PV-118204.x12");
                    Submit("S5001", "S5001_INVOIC_KC-INV-260455.edi");
                    _host.Platform.PollFolders();
                    _host.Partners.CollectFolders();
                    _host.Erp.ProcessInbound();
                });

                Step("next day 12:00 ack timeouts and SLA monitor", 12, 0, () =>
                {
                    _host.Orchestrations.CheckAckTimeouts();
                    Sla();
                });

                Verify();
            }

            Report(workFolder);
            return _checks.All(c => c.Passed) ? 0 : 1;
        }

        private void Step(string title, int hour, int minute, Action action)
        {
            _lab.Now = _lab.Now.Date.AddHours(hour).AddMinutes(minute);
            Console.WriteLine();
            Console.WriteLine("== " + _lab.Now.ToString("yyyy-MM-dd ", CultureInfo.InvariantCulture) + title);
            action();
        }

        private void Submit(string partnerId, string sample)
        {
            var partner = _lab.Directory.Get(partnerId);
            _host.Partners.Submit(partner, File.ReadAllBytes(Path.Combine(ScenarioPaths.Samples, sample)), sample);
        }

        /// <summary>What the WCF gateway forwards after its checks: the batch, one-way, to rl_Gateway_Orders_WCF.</summary>
        private void Gateway(string sample)
        {
            var ctx = new MessageContext();
            ctx.Promote("InboundTransportLocation", Contracts.ContextProperties.SystemNamespace, "/B2BReceive/PartnerOrders.svc");
            ctx.Write("GatewayReceiptId", Contracts.ContextProperties.VanArsdelNamespace, "GW" + _lab.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
            _host.Platform.Receive("rl_Gateway_Orders_WCF", File.ReadAllBytes(Path.Combine(ScenarioPaths.Samples, sample)), ctx);
        }

        private void Sla()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            var definitions = JsonSerializer.Deserialize<List<SlaDefinition>>(File.ReadAllText(Path.Combine(ScenarioPaths.Seed, "sla-definitions.json")), options);
            var snapshot = new MonitoringSnapshot { TakenUtc = _lab.UtcNow };
            lock (_lab.Interchanges)
            {
                snapshot.OutboundAwaitingAck.AddRange(_lab.Interchanges.Where(i => i.Direction == InterchangeDirection.Outbound && i.AckStatus == AckStatus.Pending));
            }

            snapshot.OpenOrders.AddRange(_lab.Bam.Values.Where(o => !o.InvoicedUtc.HasValue && !o.RejectedUtc.HasValue));
            foreach (var host in new[] { "B2BReceiveHost", "B2BSendHost", "B2BProcessingHost", "B2BAS2Host" })
            {
                snapshot.HostInstances.Add(new HostInstanceStatus { HostName = host, ServerName = "BTS01", State = HostInstanceState.Running });
            }

            snapshot.Partners.AddRange(_lab.Directory.All.Where(p => p.IsCustomer).Select(p => new PartnerActivity
            {
                PartnerId = p.PartnerId,
                ExpectsDailyTraffic = p.Transport == TransportKind.As2,
                LastReceivedUtc = _lab.Bam.Values.Where(o => o.PartnerId == p.PartnerId).Max(o => o.ReceivedUtc)
            }));

            var alerts = new SlaEvaluator(BusinessCalendar.Default).Evaluate(snapshot, definitions);
            foreach (var alert in alerts)
            {
                _lab.Trace("SLA {0}", alert);
            }

            _alerts = alerts;
        }

        private IList<Alert> _alerts = new List<Alert>();

        private void Verify()
        {
            var runs = _host.Orchestrations.Runs;
            var erp = _host.Erp;
            Check("ERP booked the released customer orders",
                new[] { "HW-55012", "BF-2026-0311", "BF-2026-0312", "BW-778812", "NB-26-0412", "RW-0302-01", "RW-0302-03" }.All(n => erp.Orders.Any(o => o.CustomerReference == n)),
                string.Join(", ", erp.Orders.Select(o => o.CustomerReference)));
            Check("credit check approved Brightline's large order", erp.Orders.Any(o => o.CustomerReference == "BF-2026-0312") && runs.Any(r => r.Document == "BF-2026-0312" && r.Result.Contains("CreditApproved")),
                Find(runs, "BF-2026-0312"));
            Check("credit holds for Ridgeway (limit) and Lakeshore (blocked)",
                _lab.Bam.TryGetValue("C3001|RW-0302-02", out var rw) && rw.CreditHoldUtc.HasValue && _lab.Bam.TryGetValue("C3002|LE-7781", out var le) && le.CreditHoldUtc.HasValue,
                Find(runs, "RW-0302-02") + " / " + Find(runs, "LE-7781"));
            Check("manual review for Hartwell's order with an unknown item", runs.Any(r => r.Document == "HW-55013" && r.Result.Contains("Manual review", StringComparison.Ordinal))
                && Mails().Any(m => m.Contains("Rejected order C1001 HW-55013", StringComparison.Ordinal)), Find(runs, "HW-55013"));
            Check("ERP refused the order with an unknown material", erp.Errors.Any(e => e.Contains("EL-99999", StringComparison.Ordinal)), string.Join("; ", erp.Errors));
            Check("duplicate interchange suspended", _lab.Suspended.Any(s => s.Contains("Duplicate", StringComparison.Ordinal)), string.Join("; ", _lab.Suspended));
            Check("ASN sent to Hartwell, Brightline and Bauwerk",
                new[] { ("C1001", "856"), ("C1002", "856"), ("C2001", "DESADV") }.All(x => Got(x.Item1, x.Item2)), Partners());
            Check("no ASN for Nordvik (routing rule NordvikNoAsn)", !Got("C2002", "DESADV") && runs.Any(r => r.Orchestration == "SendCustomerDocument" && r.PartnerId == "C2002"), Find(runs, "C2002"));
            Check("invoices sent to the EDI customers", new[] { ("C1001", "810"), ("C1002", "810"), ("C2001", "INVOIC"), ("C2002", "INVOIC") }.All(x => Got(x.Item1, x.Item2)), Partners());
            Check("purchase orders sent to both suppliers", Got("S5001", "ORDERS") && Got("S5002", "850"), Partners());
            Check("functional acks returned to the customers", Got("C1001", "997") && Got("C1002", "997") && Got("C2001", "CONTRL"), Partners());

            List<InterchangeRecord> outbound;
            lock (_lab.Interchanges)
            {
                outbound = _lab.Interchanges.Where(i => i.Direction == InterchangeDirection.Outbound && i.TransactionType != "997" && i.TransactionType != "CONTRL").ToList();
            }

            var pending = outbound.Where(i => i.AckStatus == AckStatus.Pending).ToList();
            Check("every outbound interchange that asks for an ack got one, except the silent supplier",
                pending.All(i => i.PartnerId == "S5001") && outbound.Count(i => i.AckStatus == AckStatus.Accepted) >= 7,
                string.Join(", ", outbound.Select(i => i.PartnerId + "/" + i.TransactionType + "/" + i.ControlNumber + "=" + i.AckStatus)));
            Check("missing acknowledgement mail for the Kestrel purchase order", Mails().Any(m => m.Contains("Missing acknowledgement S5001", StringComparison.Ordinal)), string.Join(" | ", Mails()));
            Check("supplier invoices parked in the ERP", erp.PostedSupplierInvoices.Count == 2, string.Join(", ", erp.PostedSupplierInvoices));
            Check("BAM lifecycle complete for HW-55012",
                _lab.Bam.TryGetValue("C1001|HW-55012", out var hw) && hw.Status == "Invoiced" && hw.AsnSentUtc.HasValue && !string.IsNullOrEmpty(hw.ErpOrderNumber),
                hw == null ? "no activity" : hw.Status + " erp=" + hw.ErpOrderNumber + " asn=" + hw.AsnSentUtc);
            Check("SLA monitor raises the overdue ack and the credit holds",
                _alerts.Any(a => a.Kind == SlaKind.AckOverdue && a.PartnerId == "S5001") && _alerts.Count(a => a.Kind == SlaKind.CreditHoldAging) == 2,
                string.Join(" | ", _alerts.Select(a => a.SlaCode + " " + a.PartnerId)));
        }

        private bool Got(string partnerId, string type)
        {
            return _host.Partners.Received.Any(r => r.PartnerId == partnerId && r.TransactionTypes.Contains(type));
        }

        private string Partners()
        {
            return string.Join(", ", _host.Partners.Received.GroupBy(r => r.PartnerId).Select(g => g.Key + ":" + string.Join("+", g.SelectMany(r => r.TransactionTypes))));
        }

        private IEnumerable<string> Mails()
        {
            return Directory.GetFiles(_lab.WorkPath("mail"), "*.eml").Select(f => File.ReadLines(f).FirstOrDefault(l => l.StartsWith("Subject:", StringComparison.Ordinal)) ?? string.Empty);
        }

        private static string Find(IEnumerable<OrchestrationRun> runs, string text)
        {
            return string.Join(" | ", runs.Where(r => r.Document == text || r.PartnerId == text).Select(r => r.ToString()));
        }

        private void Check(string name, bool passed, string detail)
        {
            _checks.Add((name, passed, detail));
        }

        private void Report(string workFolder)
        {
            Console.WriteLine();
            Console.WriteLine("== orchestration instances");
            foreach (var run in _host.Orchestrations.Runs)
            {
                Console.WriteLine("  " + run);
            }

            Console.WriteLine();
            Console.WriteLine("== OrderLifecycle (BAM)");
            foreach (var row in _lab.Bam.Values.OrderBy(r => r.ActivityId, StringComparer.Ordinal))
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,-22} {1,-12} erp={2,-6} value={3,10:0.00} {4}", row.ActivityId, row.Status, row.ErpOrderNumber?.TrimStart('0'), row.OrderValue,
                    row.HoldReason ?? row.RejectReason ?? string.Empty));
            }

            Console.WriteLine();
            Console.WriteLine("== checks");
            foreach (var c in _checks)
            {
                Console.WriteLine((c.Passed ? "  PASS " : "  FAIL ") + c.Name + (c.Passed ? string.Empty : Environment.NewLine + "       " + c.Detail));
            }

            File.WriteAllLines(Path.Combine(workFolder, "lab.log"), _lab.Log);
            Console.WriteLine();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} of {1} checks passed; trace in {2}", _checks.Count(c => c.Passed), _checks.Count, Path.Combine(workFolder, "lab.log")));
        }
    }
}
