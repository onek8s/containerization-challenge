using System;
using System.IO;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.LabStubs;
using Tailspin.DealerOrders.Processing;
using Tailspin.DealerOrders.Processing.Ax;
using Tailspin.DealerOrders.Processing.Catalog;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.Processing.Sync;

namespace Tailspin.DealerOrders.LabTools
{
    /// <summary>
    /// Walks one portal order and one EDI order through quote, approval, AX, confirmation, shipment and invoicing with
    /// in-memory stores and the AX simulator on a simulated clock. Used to show the process to new desk staff.
    /// </summary>
    internal static class FlowCommand
    {
        public static int Run(Options options)
        {
            var clock = new FixedClock(new DateTime(2019, 10, 15, 8, 0, 0, DateTimeKind.Utc));
            var stores = InMemoryStores.WithLabData();
            var catalog = new FileCatalog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalog"));
            var settings = new ProcessingSettings();
            var ax = new AxSimulator(new AxSimulatorOptions { UtcNow = () => clock.UtcNow });
            var prices = new PriceCalculator(stores);
            var leadTimes = new LeadTimeCalculator();
            var notifications = new NotificationComposer(stores, clock, settings);
            var commands = new AxCommandFactory(stores, clock);
            var quotes = new QuoteService(stores, stores, catalog, stores, new ConfigurationEngine(), prices, leadTimes, commands, notifications, clock, settings);
            var orders = new OrderService(stores, stores, stores, catalog, stores, quotes, prices, commands, notifications, clock, settings);
            var approvals = new ApprovalService(stores, stores, stores, stores, quotes, orders, notifications, prices, clock);
            var ediOut = new EdiOutbound(stores, clock, settings);
            var statusSync = new OrderStatusSync(stores, stores, stores, notifications, ediOut, clock);
            var processor = new AxCommandProcessor(stores, ax, ax, stores, stores, stores, new AxDocumentMapper(catalog), statusSync, notifications, clock, settings);
            var poller = new ChangedOrderPoller(ax, ax, stores, stores, statusSync, clock, settings);
            var intake = new EdiOrderIntake(stores, stores, stores, catalog, orders, ediOut, clock);
            new CacheSync(ax, stores, stores, clock, settings).SyncPrices();

            void Elapse(int minutes)
            {
                clock.Advance(TimeSpan.FromMinutes(minutes));
                processor.RunDue(100);
                poller.Run();
            }

            var dealer = stores.All().Single(d => d.AccountNumber == "D-10010");
            var request = new ConfigurationRequest { ModelCode = "TF14", Options = { "CRANE-11M", "BOGIE-HD", "LED-PACK" } };
            var evaluation = quotes.Evaluate(dealer, request);
            Step($"Configured TF14 for {dealer.Name}: {evaluation.ConfigurationId}, net {evaluation.NetTotal:N2} {evaluation.Currency}, {evaluation.LeadTimeWeeks} weeks");

            var quote = quotes.CreateFromConfiguration(dealer.Id, "mkp.sales", request, "Lapin Metsäpalvelu Oy", "FI", "MKP-1907");
            quotes.Submit(quote.Id, "mkp.sales", 5m);
            Step($"Quote {quote.QuoteNumber} submitted with 5 % extra discount: {quote.Status}");

            var approval = stores.Pending().Single();
            approvals.Decide(approval.Id, @"CORP\anna.virtanen", false, true, 4m, "Competitive deal", "annvir");
            processor.RunDue(100);
            Step($"Sales desk approved 4 %; quote registered in AX as {quote.AxQuotationId}");

            var order = orders.AcceptQuote(quote.Id, "mkp.sales", "MKP-PO-5521", null, null);
            processor.RunDue(100);
            Step($"Order {order.OrderNumber} created in AX as {order.AxSalesId}: {order.Status}");

            var po = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "edi", "850-glf-machine.x12"));
            var result = intake.Receive(po, "lab-as2-1");
            processor.RunDue(100);
            Step($"850 from Great Lakes: orders {string.Join(", ", result.CreatedOrders)}, 997 queued for {result.InterchangeControl}");

            foreach (var minutes in new[] { 3, 3, 5, 5 })
            {
                Elapse(minutes);
                Step($"+{minutes} min: {order.OrderNumber} {order.Status}" + (order.ConfirmedDeliveryDate.HasValue ? $", delivery {order.ConfirmedDeliveryDate:yyyy-MM-dd}" : ""));
            }

            Step("Outbound EDI: " + string.Join(", ", stores.Messages.Where(m => m.Direction == EdiDirection.Outbound).Select(m => m.TransactionSet + " " + m.Reference)));
            Step("Notifications: " + string.Join(", ", stores.Notifications.GroupBy(n => n.Kind).Select(g => g.Key + " x" + g.Count())));
            return order.Status == OrderStatus.Invoiced ? 0 : 6;
        }

        private static void Step(string text) => Console.WriteLine("  " + text);
    }
}
