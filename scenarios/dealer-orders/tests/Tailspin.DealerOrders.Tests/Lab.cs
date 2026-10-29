using System;
using System.IO;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.LabStubs;
using Tailspin.DealerOrders.Processing;
using Tailspin.DealerOrders.Processing.Ax;
using Tailspin.DealerOrders.Processing.Catalog;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.Processing.Sync;

namespace Tailspin.DealerOrders.Tests
{
    /// <summary>The processing services wired to in-memory stores and the AX simulator, with a controllable clock.</summary>
    public sealed class Lab
    {
        public static readonly string CatalogFolder = Path.Combine(AppContext.BaseDirectory, "catalog");

        public Lab(DateTime? utcNow = null, int unavailableEvery = 0)
        {
            Clock = new FixedClock(utcNow ?? new DateTime(2019, 10, 15, 8, 0, 0, DateTimeKind.Utc));
            Stores = InMemoryStores.WithLabData();
            Catalog = new FileCatalog(CatalogFolder);
            Settings = new ProcessingSettings();
            Ax = new AxSimulator(new AxSimulatorOptions { UtcNow = () => Clock.UtcNow, UnavailableEvery = unavailableEvery });
            Engine = new ConfigurationEngine();
            Prices = new PriceCalculator(Stores);
            LeadTimes = new LeadTimeCalculator();
            Notifications = new NotificationComposer(Stores, Clock, Settings);
            Commands = new AxCommandFactory(Stores, Clock);
            Quotes = new QuoteService(Stores, Stores, Catalog, Stores, Engine, Prices, LeadTimes, Commands, Notifications, Clock, Settings);
            Orders = new OrderService(Stores, Stores, Stores, Catalog, Stores, Quotes, Prices, Commands, Notifications, Clock, Settings);
            Approvals = new ApprovalService(Stores, Stores, Stores, Stores, Quotes, Orders, Notifications, Prices, Clock);
            EdiOut = new EdiOutbound(Stores, Clock, Settings);
            StatusSync = new OrderStatusSync(Stores, Stores, Stores, Notifications, EdiOut, Clock);
            Processor = new AxCommandProcessor(Stores, Ax, Ax, Stores, Stores, Stores, new AxDocumentMapper(Catalog), StatusSync, Notifications, Clock, Settings);
            Poller = new ChangedOrderPoller(Ax, Ax, Stores, Stores, StatusSync, Clock, Settings);
            EdiIn = new EdiOrderIntake(Stores, Stores, Stores, Catalog, Orders, EdiOut, Clock);
            CacheSync = new CacheSync(Ax, Stores, Stores, Clock, Settings) { PageSize = 25 };
        }

        public FixedClock Clock { get; }
        public InMemoryStores Stores { get; }
        public FileCatalog Catalog { get; }
        public ProcessingSettings Settings { get; }
        public AxSimulator Ax { get; }
        public ConfigurationEngine Engine { get; }
        public PriceCalculator Prices { get; }
        public LeadTimeCalculator LeadTimes { get; }
        public NotificationComposer Notifications { get; }
        public AxCommandFactory Commands { get; }
        public QuoteService Quotes { get; }
        public OrderService Orders { get; }
        public ApprovalService Approvals { get; }
        public EdiOutbound EdiOut { get; }
        public OrderStatusSync StatusSync { get; }
        public AxCommandProcessor Processor { get; }
        public ChangedOrderPoller Poller { get; }
        public EdiOrderIntake EdiIn { get; }
        public CacheSync CacheSync { get; }

        public int RunAx() => Processor.RunDue(100);

        /// <summary>Advances time and lets AX move the orders along, then reads the changes back.</summary>
        public void Elapse(TimeSpan time)
        {
            Clock.Advance(time);
            RunAx();
            Poller.Run();
        }

        public static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

        public Domain.Dealer Dealer(string account) => Stores.All().Single(d => d.AccountNumber == account);
    }
}
