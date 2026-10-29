using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using log4net;
using Newtonsoft.Json;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.LabStubs
{
    public class AxSimulatorOptions
    {
        /// <summary>JSON file holding the simulated AX data; empty keeps everything in memory (tests).</summary>
        public string StateFile { get; set; }

        /// <summary>Simulated order life cycle, measured from order creation.</summary>
        public TimeSpan ConfirmAfter { get; set; } = TimeSpan.FromMinutes(2);
        public TimeSpan ProductionAfter { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan ShipAfter { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan InvoiceAfter { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>Every Nth call fails as if the AOS were unreachable (0 = never).</summary>
        public int UnavailableEvery { get; set; }

        public int MachineLeadWeeks { get; set; } = 14;
        public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    }

    /// <summary>
    /// Stands in for Dynamics AX in test and training environments: customers, configurations, quotations, sales
    /// orders, reservations and a simple order life cycle (confirmation, production, packing slip, invoice) driven by
    /// elapsed time. Behaves like the TSDealerOrderService X++ class for the cases the integration relies on, including
    /// idempotent order creation by PurchOrderFormNum and AX-style infolog errors.
    /// </summary>
    public class AxSimulator : IAxSalesGateway, IAxDocumentReader, IAxQueryReader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AxSimulator));
        private static readonly object FileGate = new object();

        private readonly AxSimulatorOptions _options;
        private AxSimulatorState _memory;

        public AxSimulator(AxSimulatorOptions options)
        {
            _options = options;
        }

        public static AxSimulatorState InitialState()
        {
            var state = new AxSimulatorState();
            foreach (var dealer in LabData.Dealers())
            {
                var customer = Processing.Ax.AxDocumentMapper.Customer(dealer);
                customer.Blocked = dealer.CreditHold ? AxCustomerBlocked.Invoice : AxCustomerBlocked.No;
                customer.Addresses.Clear();
                state.Customers[dealer.AccountNumber] = new SimCustomer { Customer = customer, Balance = dealer.OpenBalance };
            }
            foreach (var part in LabData.EuroListPrices.Keys.Where(k => k.StartsWith("P-", StringComparison.Ordinal)))
            {
                AddStock(state, part, "", "FI-KON", 40, 0);
                AddStock(state, part, "", "US-DUL", part == "P-20310" ? 0 : 12, part == "P-20310" ? 4 : 0);
            }
            AddStock(state, "TL450", "TL45GBWP2M", "FI-KON", 3, 0);
            AddStock(state, "TF10", "TF10EK7Q1R", "US-DUL", 1, 1);
            return state;
        }

        private static void AddStock(AxSimulatorState state, string item, string config, string warehouse, decimal onHand, decimal onOrder) =>
            state.Stock[SimStock.Key(item, config, warehouse)] = new SimStock { ItemId = item, ConfigId = config, Warehouse = warehouse, OnHand = onHand, OnOrder = onOrder };

        // ---- IAxSalesGateway -------------------------------------------------------------------------------------

        public AxCustomerResult EnsureCustomer(AxCustomer customer, AxCallContext context) => Write(state =>
        {
            var result = new AxCustomerResult { AccountNum = customer.AccountNum };
            if (!state.Customers.TryGetValue(customer.AccountNum, out var existing))
            {
                existing = new SimCustomer { Customer = new AxCustomer { AccountNum = customer.AccountNum } };
                state.Customers[customer.AccountNum] = existing;
                result.Created = true;
            }

            var blocked = existing.Customer.Blocked;
            var addresses = existing.Customer.Addresses;
            existing.Customer = customer;
            existing.Customer.Blocked = blocked == AxCustomerBlocked.No ? customer.Blocked : blocked;
            foreach (var address in customer.Addresses)
            {
                var match = addresses.FirstOrDefault(a => a.ExternalCode == address.ExternalCode);
                address.LocationId = address.LocationId ?? match?.LocationId ?? "LOC" + state.Next("location", 100000).ToString("000000", CultureInfo.InvariantCulture);
                result.LocationIdsByExternalCode[address.ExternalCode] = address.LocationId;
            }
            result.Infolog.Add(result.Created ? $"Customer {customer.AccountNum} created." : $"Customer {customer.AccountNum} updated.");
            return result;
        });

        public void EnsureConfiguration(string itemId, string configId, IList<string> optionItemIds, AxCallContext context) => Write(state =>
        {
            RequireItem(itemId);
            foreach (var option in optionItemIds) RequireItem(option);
            state.Configurations.Add(itemId + "|" + configId);
            return true;
        });

        public AxDocumentResult CreateQuotation(AxSalesDocument quotation, AxCallContext context) => Write(state =>
        {
            var existing = state.Quotations.Values.FirstOrDefault(q => q.Document.PortalReference == quotation.PortalReference);
            if (existing != null) return Result(existing);
            RequireCustomer(state, quotation.CustAccount);
            var id = "SQ" + Year() + "-" + state.Next("quotation", 0).ToString("00000", CultureInfo.InvariantCulture);
            var doc = NewDocument(state, id, quotation);
            state.Quotations[id] = doc;
            return Result(doc);
        });

        public AxDocumentResult ConvertQuotation(string quotationId, string customerRef, AxCallContext context) => Write(state =>
        {
            if (!state.Quotations.TryGetValue(quotationId ?? "", out var quotation))
                throw new AxBusinessException("Quotation could not be converted.", new[] { $"Quotation {quotationId} does not exist." });
            var existing = state.SalesOrders.Values.FirstOrDefault(o => o.QuotationId == quotationId);
            if (existing != null) return Result(existing);
            RequireCustomer(state, quotation.Document.CustAccount);
            quotation.DocumentStatus = AxDocumentStatus.Quotation;
            var document = JsonConvert.DeserializeObject<AxSalesDocument>(JsonConvert.SerializeObject(quotation.Document));
            document.CustomerRef = customerRef;
            var id = NextSalesId(state);
            var order = NewDocument(state, id, document);
            order.QuotationId = quotationId;
            state.SalesOrders[id] = order;
            Log.InfoFormat("Simulated AX: quotation {0} converted to {1}", quotationId, id);
            return Result(order);
        });

        public AxDocumentResult CreateSalesOrder(AxSalesDocument order, AxCallContext context) => Write(state =>
        {
            var existing = state.SalesOrders.Values.FirstOrDefault(o => o.Document.PortalReference == order.PortalReference);
            if (existing != null)
            {
                Log.InfoFormat("Simulated AX: {0} already exists as {1}", order.PortalReference, existing.Id);
                return Result(existing);
            }
            RequireCustomer(state, order.CustAccount);
            foreach (var line in order.Lines)
            {
                RequireItem(line.ItemId);
                if (line.Quantity <= 0) throw new AxBusinessException("Update has been canceled.", new[] { $"Quantity on line {line.LineNumber} must be positive." });
                if (!string.IsNullOrEmpty(line.ConfigId) && !state.Configurations.Contains(line.ItemId + "|" + line.ConfigId))
                    throw new AxBusinessException("Update has been canceled.", new[] { $"Configuration {line.ConfigId} does not exist for item {line.ItemId}." });
            }
            var id = NextSalesId(state);
            var doc = NewDocument(state, id, order);
            state.SalesOrders[id] = doc;
            Log.InfoFormat("Simulated AX: sales order {0} created for {1} ({2})", id, order.CustAccount, order.PortalReference);
            return Result(doc);
        });

        public AxDocumentResult UpdateSalesOrder(string salesId, AxSalesDocument changes, AxCallContext context) => Write(state =>
        {
            var doc = Order(state, salesId);
            if (doc.Status != AxSalesStatus.Backorder) throw new AxBusinessException("Update has been canceled.", new[] { $"Sales order {salesId} has been delivered and cannot be changed." });
            doc.Document.DeliveryDate = changes.DeliveryDate;
            doc.Document.CustomerRef = changes.CustomerRef;
            foreach (var line in changes.Lines)
            {
                var existing = doc.Lines.FirstOrDefault(l => l.InventTransId == line.InventTransId);
                if (existing == null)
                {
                    doc.Lines.Add(NewLine(state, line));
                    continue;
                }
                if (line.Delete) existing.Cancelled = true;
                existing.Quantity = line.Quantity;
                existing.SalesPrice = line.SalesPrice;
                existing.LinePercent = line.LinePercent;
            }
            if (changes.DeliveryDate.HasValue && doc.ConfirmedDate.HasValue && changes.DeliveryDate.Value > doc.ConfirmedDate.Value)
                doc.ConfirmedDate = changes.DeliveryDate;
            doc.ModifiedUtc = _options.UtcNow();
            return Result(doc);
        });

        public void CancelSalesOrder(string salesId, string reason, AxCallContext context) => Write(state =>
        {
            var doc = Order(state, salesId);
            if (doc.Lines.Any(l => l.Delivered > 0))
                throw new AxBusinessException("Update has been canceled.", new[] { $"Sales order {salesId} has posted packing slips and cannot be cancelled." });
            ReleaseInternal(state, doc);
            doc.Status = AxSalesStatus.Canceled;
            foreach (var line in doc.Lines) line.Cancelled = true;
            doc.ModifiedUtc = _options.UtcNow();
            return true;
        });

        public IList<AxReservationResult> Reserve(string salesId, AxCallContext context) => Write(state =>
        {
            var doc = Order(state, salesId);
            var results = new List<AxReservationResult>();
            foreach (var line in doc.Lines.Where(l => !l.Cancelled))
            {
                var key = SimStock.Key(line.ItemId, line.ConfigId, doc.Document.InventLocationId);
                state.Stock.TryGetValue(key, out var stock);
                var free = stock == null ? 0 : stock.OnHand - stock.Reserved;
                var wanted = line.Quantity - line.Reserved - line.Delivered;
                var take = Math.Max(0, Math.Min(free, wanted));
                if (stock != null) stock.Reserved += take;
                line.Reserved += take;
                results.Add(new AxReservationResult
                {
                    InventTransId = line.InventTransId,
                    ItemId = line.ItemId,
                    Requested = line.Quantity,
                    Reserved = line.Reserved,
                    ExpectedDate = line.Reserved < line.Quantity ? _options.UtcNow().Date.AddDays(stock != null && stock.OnOrder > 0 ? 10 : 28) : (DateTime?)null
                });
            }
            doc.ModifiedUtc = _options.UtcNow();
            return (IList<AxReservationResult>)results;
        });

        public void ReleaseReservation(string salesId, AxCallContext context) => Write(state =>
        {
            ReleaseInternal(state, Order(state, salesId));
            return true;
        });

        // ---- IAxDocumentReader -----------------------------------------------------------------------------------

        public AxSalesOrderSnapshot ReadSalesOrder(string salesId, AxCallContext context) => Write(state =>
        {
            Advance(state);
            var doc = Order(state, salesId);
            return new AxSalesOrderSnapshot
            {
                SalesId = doc.Id,
                CustAccount = doc.Document.CustAccount,
                CustomerRef = doc.Document.CustomerRef,
                SalesStatus = doc.Status,
                DocumentStatus = doc.DocumentStatus,
                ConfirmedDeliveryDate = doc.ConfirmedDate,
                InProduction = doc.InProduction,
                ModifiedDateTime = doc.ModifiedUtc,
                Lines = doc.Lines.Select(l => new AxSalesLineSnapshot
                {
                    InventTransId = l.InventTransId,
                    ItemId = l.ItemId,
                    ConfigId = l.ConfigId,
                    QtyOrdered = l.Quantity,
                    ReservedPhysical = l.Reserved,
                    DeliveredQty = l.Delivered,
                    InvoicedQty = l.Invoiced,
                    ConfirmedShipDate = l.ShipDate,
                    SerialId = l.SerialId,
                    LineStatus = l.Cancelled ? AxSalesStatus.Canceled : l.Invoiced >= l.Quantity ? AxSalesStatus.Invoiced : l.Delivered >= l.Quantity ? AxSalesStatus.Delivered : AxSalesStatus.Backorder
                }).ToList(),
                PackingSlips = doc.PackingSlips.ToList(),
                InvoiceIds = doc.InvoiceIds.ToList()
            };
        });

        public AxCustomer ReadCustomer(string accountNum, AxCallContext context) => Read(state =>
            state.Customers.TryGetValue(accountNum, out var c) ? c.Customer : throw new AxBusinessException("Read failed.", new[] { $"Customer {accountNum} does not exist." }));

        public IList<AxStockRecord> FindOnHand(string itemId, string inventLocationId, AxCallContext context) => Read(state =>
            (IList<AxStockRecord>)state.Stock.Values.Where(s => s.ItemId == itemId && (string.IsNullOrEmpty(inventLocationId) || s.Warehouse == inventLocationId)).Select(ToRecord).ToList());

        // ---- IAxQueryReader --------------------------------------------------------------------------------------

        public AxPage<AxPriceRecord> ReadSalesPrices(int startPosition, int pageSize, AxCallContext context) => Read(state =>
            Page(LabData.Prices().Select(p => new AxPriceRecord
            {
                ItemRelation = p.ItemId,
                AccountRelation = p.PriceGroup == "LIST" ? "" : p.PriceGroup,
                Currency = p.Currency,
                Amount = p.Amount,
                QuantityAmountFrom = p.QuantityFrom,
                FromDate = p.FromDate,
                ToDate = p.ToDate
            }).ToList(), startPosition, pageSize));

        public AxPage<AxStockRecord> ReadAvailability(int startPosition, int pageSize, AxCallContext context) => Read(state =>
            Page(state.Stock.Values.OrderBy(s => s.ItemId).ThenBy(s => s.Warehouse).Select(ToRecord).ToList(), startPosition, pageSize));

        public AxPage<AxCustomerBalance> ReadCustomerBalances(int startPosition, int pageSize, AxCallContext context) => Write(state =>
        {
            Advance(state);
            return Page(state.Customers.Values.OrderBy(c => c.Customer.AccountNum).Select(c => new AxCustomerBalance
            {
                AccountNum = c.Customer.AccountNum,
                Balance = c.Balance,
                CreditMax = c.Customer.CreditMax,
                Blocked = c.Customer.Blocked,
                OpenOrderValue = state.SalesOrders.Values.Where(o => o.Document.CustAccount == c.Customer.AccountNum && o.Status == AxSalesStatus.Backorder).Sum(o => o.Lines.Sum(l => l.NetAmount))
            }).ToList(), startPosition, pageSize);
        });

        public AxPage<AxOrderHeaderStatus> ReadChangedOrders(DateTime changedSinceUtc, int startPosition, int pageSize, AxCallContext context) => Write(state =>
        {
            Advance(state);
            return Page(state.SalesOrders.Values.Where(o => o.ModifiedUtc >= changedSinceUtc).OrderBy(o => o.ModifiedUtc).Select(o => new AxOrderHeaderStatus
            {
                SalesId = o.Id,
                PortalReference = o.Document.PortalReference,
                SalesStatus = o.Status,
                DocumentStatus = o.DocumentStatus,
                ModifiedDateTime = o.ModifiedUtc
            }).ToList(), startPosition, pageSize);
        });

        public AxPage<AxExchangeRateRecord> ReadExchangeRates(int startPosition, int pageSize, AxCallContext context) => Read(state =>
            Page(LabData.ExchangeRates().Select(r => new AxExchangeRateRecord { FromCurrency = r.FromCurrency, ToCurrency = r.ToCurrency, Rate = r.Rate, ValidFrom = r.ValidFrom }).ToList(),
                startPosition, pageSize));

        public AxPage<AxItemRecord> ReadItems(int startPosition, int pageSize, AxCallContext context) => Read(state =>
            Page(LabData.EuroListPrices.Keys.OrderBy(k => k).Select(k => new AxItemRecord
            {
                ItemId = k,
                Name = k,
                ItemGroup = k.StartsWith("P-", StringComparison.Ordinal) ? "PARTS" : k.StartsWith("OPT-", StringComparison.Ordinal) ? "OPTIONS" : "MACHINES",
                NetWeightKg = k.StartsWith("P-", StringComparison.Ordinal) ? 2.5m : 0m
            }).ToList(), startPosition, pageSize));

        // ---- life cycle ------------------------------------------------------------------------------------------

        /// <summary>Moves orders along: confirmation, production, packing slip, invoice, by time since creation.</summary>
        public void Advance(AxSimulatorState state)
        {
            var now = _options.UtcNow();
            foreach (var doc in state.SalesOrders.Values.Where(o => o.Status != AxSalesStatus.Canceled && o.Status != AxSalesStatus.Invoiced))
            {
                var age = now - doc.CreatedUtc;
                var machines = doc.Lines.Any(l => l.IsMachine);
                if (doc.DocumentStatus < AxDocumentStatus.Confirmation && age >= _options.ConfirmAfter)
                {
                    var lead = machines ? _options.MachineLeadWeeks * 7 : 5;
                    var date = doc.Document.DeliveryDate ?? now.Date.AddDays(lead);
                    if (date < now.Date.AddDays(machines ? lead : 2)) date = now.Date.AddDays(machines ? lead : 2);
                    while (date.DayOfWeek != DayOfWeek.Thursday) date = date.AddDays(1);
                    doc.ConfirmedDate = date;
                    foreach (var line in doc.Lines) line.ShipDate = date;
                    doc.DocumentStatus = AxDocumentStatus.Confirmation;
                    doc.ModifiedUtc = now;
                }
                if (machines && !doc.InProduction && doc.DocumentStatus == AxDocumentStatus.Confirmation && age >= _options.ProductionAfter)
                {
                    doc.InProduction = true;
                    doc.ModifiedUtc = now;
                }
                if (doc.PackingSlips.Count == 0 && age >= _options.ShipAfter && doc.DocumentStatus >= AxDocumentStatus.Confirmation)
                {
                    var slip = new AxPackingSlip
                    {
                        PackingSlipId = "PS" + Year() + "-" + state.Next("packingslip", 0).ToString("000000", CultureInfo.InvariantCulture),
                        DeliveryDate = now.Date,
                        CarrierCode = doc.Document.InventLocationId == "US-DUL" ? "ODFL" : "DSVR",
                        TrackingNumber = (doc.Document.InventLocationId == "US-DUL" ? "7" : "DSV") + state.Next("tracking", 400000000).ToString(CultureInfo.InvariantCulture),
                        TrailerNumber = machines ? "TRL-" + (state.Calls % 900 + 100) : null,
                        Packages = machines ? doc.Lines.Where(l => l.IsMachine).Sum(l => (int)l.Quantity) : 1
                    };
                    foreach (var line in doc.Lines.Where(l => !l.Cancelled))
                    {
                        var qty = line.IsMachine ? line.Quantity : (line.Reserved > 0 ? line.Reserved : line.Quantity);
                        if (line.IsMachine)
                            line.SerialId = line.ItemId + Year() + state.Next("serial-" + line.ItemId, 10000).ToString("00000", CultureInfo.InvariantCulture);
                        line.Delivered = qty;
                        Consume(state, doc, line, qty);
                        slip.Lines.Add(new AxPackingSlipLine { InventTransId = line.InventTransId, ItemId = line.ItemId, Quantity = qty, SerialId = line.SerialId });
                        slip.GrossWeightKg += line.IsMachine ? 18500m * qty : 2.5m * qty;
                    }
                    doc.PackingSlips.Add(slip);
                    doc.DocumentStatus = AxDocumentStatus.PackingSlip;
                    doc.InProduction = false;
                    doc.Status = doc.Lines.Where(l => !l.Cancelled).All(l => l.Delivered >= l.Quantity) ? AxSalesStatus.Delivered : AxSalesStatus.Backorder;
                    doc.ModifiedUtc = now;
                }
                if (doc.Status == AxSalesStatus.Delivered && age >= _options.InvoiceAfter)
                {
                    doc.InvoiceIds.Add("INV" + Year() + "-" + state.Next("invoice", 0).ToString("000000", CultureInfo.InvariantCulture));
                    foreach (var line in doc.Lines) line.Invoiced = line.Delivered;
                    doc.Status = AxSalesStatus.Invoiced;
                    doc.DocumentStatus = AxDocumentStatus.Invoice;
                    if (state.Customers.TryGetValue(doc.Document.CustAccount, out var customer)) customer.Balance += doc.Lines.Sum(l => l.NetAmount);
                    doc.ModifiedUtc = now;
                }
            }
        }

        private static void Consume(AxSimulatorState state, SimDocument doc, SimLine line, decimal qty)
        {
            var key = SimStock.Key(line.ItemId, line.ConfigId, doc.Document.InventLocationId);
            if (!state.Stock.TryGetValue(key, out var stock)) return;
            stock.OnHand = Math.Max(0, stock.OnHand - qty);
            stock.Reserved = Math.Max(0, stock.Reserved - line.Reserved);
            line.Reserved = 0;
        }

        private static void ReleaseInternal(AxSimulatorState state, SimDocument doc)
        {
            foreach (var line in doc.Lines.Where(l => l.Reserved > 0))
            {
                if (state.Stock.TryGetValue(SimStock.Key(line.ItemId, line.ConfigId, doc.Document.InventLocationId), out var stock))
                    stock.Reserved = Math.Max(0, stock.Reserved - line.Reserved);
                line.Reserved = 0;
            }
        }

        private SimDocument NewDocument(AxSimulatorState state, string id, AxSalesDocument document)
        {
            var now = _options.UtcNow();
            var doc = new SimDocument { Id = id, Document = document, CreatedUtc = now, ModifiedUtc = now };
            foreach (var line in document.Lines) doc.Lines.Add(NewLine(state, line));
            return doc;
        }

        private static SimLine NewLine(AxSimulatorState state, AxSalesLineData line) => new SimLine
        {
            LineNumber = line.LineNumber,
            InventTransId = "LOT" + state.Next("inventtrans", 4000000).ToString("000000000", CultureInfo.InvariantCulture),
            ItemId = line.ItemId,
            ConfigId = line.ConfigId,
            Quantity = line.Quantity,
            SalesPrice = line.SalesPrice,
            LinePercent = line.LinePercent,
            IsMachine = !string.IsNullOrEmpty(line.ConfigId) && !line.ItemId.StartsWith("P-", StringComparison.Ordinal) && line.OptionItemIds.Count > 0
        };

        private static AxDocumentResult Result(SimDocument doc)
        {
            var result = new AxDocumentResult { DocumentId = doc.Id };
            foreach (var line in doc.Lines) result.InventTransIdsByLine[line.LineNumber] = line.InventTransId;
            result.Infolog.Add($"{doc.Id} saved.");
            return result;
        }

        private static AxStockRecord ToRecord(SimStock s) => new AxStockRecord
        {
            ItemId = s.ItemId,
            ConfigId = s.ConfigId,
            InventLocationId = s.Warehouse,
            PhysicalInvent = s.OnHand,
            ReservPhysical = s.Reserved,
            OnOrder = s.OnOrder,
            NextReceiptDate = s.OnOrder > 0 ? DateTime.UtcNow.Date.AddDays(10) : (DateTime?)null
        };

        private static AxPage<T> Page<T>(List<T> all, int start, int size)
        {
            var from = Math.Max(1, start) - 1;
            var page = new AxPage<T> { Records = all.Skip(from).Take(size).ToList() };
            page.NextPosition = from + size < all.Count ? from + size + 1 : (int?)null;
            return page;
        }

        private static void RequireItem(string itemId)
        {
            if (!LabData.EuroListPrices.ContainsKey(itemId ?? ""))
                throw new AxBusinessException("Update has been canceled.", new[] { $"Item number {itemId} does not exist." });
        }

        private static void RequireCustomer(AxSimulatorState state, string account)
        {
            if (!state.Customers.TryGetValue(account ?? "", out var customer))
                throw new AxBusinessException("Update has been canceled.", new[] { $"Customer account {account} does not exist." });
            // Invoice hold only stops invoicing in AX; orders can still be entered.
            if (customer.Customer.Blocked == AxCustomerBlocked.All)
                throw new AxBusinessException("Update has been canceled.", new[] { $"Customer {account} is on hold: All.", "Transactions on hold for customer." });
        }

        private static SimDocument Order(AxSimulatorState state, string salesId) =>
            state.SalesOrders.TryGetValue(salesId ?? "", out var doc) ? doc : throw new AxBusinessException("Read failed.", new[] { $"Sales order {salesId} does not exist." });

        private string NextSalesId(AxSimulatorState state) => "SO" + Year() + "-" + state.Next("salesorder", 4500).ToString("000000", CultureInfo.InvariantCulture);

        private string Year() => _options.UtcNow().ToString("yy", CultureInfo.InvariantCulture);

        // ---- state handling --------------------------------------------------------------------------------------

        private T Read<T>(Func<AxSimulatorState, T> action) => Use(action);

        private T Write<T>(Func<AxSimulatorState, T> action) => Use(action);

        private T Use<T>(Func<AxSimulatorState, T> action)
        {
            if (string.IsNullOrEmpty(_options.StateFile))
            {
                lock (FileGate)
                {
                    _memory = _memory ?? InitialState();
                    CheckAvailable(_memory);
                    return action(_memory);
                }
            }

            lock (FileGate)
            {
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        // The connector and the sync service share the file; FileShare.None serializes them.
                        using (var stream = new FileStream(_options.StateFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        {
                            var text = new StreamReader(stream).ReadToEnd();
                            var state = string.IsNullOrWhiteSpace(text) ? InitialState() : JsonConvert.DeserializeObject<AxSimulatorState>(text);
                            AxUnavailableException failure = null;
                            var result = default(T);
                            try
                            {
                                CheckAvailable(state);
                                result = action(state);
                            }
                            catch (AxUnavailableException ex)
                            {
                                failure = ex;
                            }
                            stream.SetLength(0);
                            var writer = new StreamWriter(stream);
                            writer.Write(JsonConvert.SerializeObject(state, Formatting.Indented));
                            writer.Flush();
                            if (failure != null) throw failure;
                            return result;
                        }
                    }
                    catch (IOException) when (attempt < 50)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }

        private void CheckAvailable(AxSimulatorState state)
        {
            state.Calls++;
            if (_options.UnavailableEvery > 0 && state.Calls % _options.UnavailableEvery == 0)
                throw new AxUnavailableException("The Business Connector session could not be started: the server aos01 could not be reached (simulated).");
        }
    }
}
