using System.Globalization;
using System.Text;
using System.Xml.Linq;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Domain.Codes;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.LabStubs.Lab;
using VanArsdel.Integration.LabStubs.Seed;

namespace VanArsdel.Integration.LabStubs.Simulators
{
    public class ErpSalesLine
    {
        public int Position { get; set; }
        public string Item { get; set; }
        public string PartnerItem { get; set; }
        public string Gtin { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Uom { get; set; }
        public decimal Price { get; set; }

        public decimal Amount => Math.Round(Quantity * Price, 2);
    }

    public class ErpSalesOrder
    {
        public string SoldTo { get; set; }
        public string CustomerReference { get; set; }
        public string ErpOrderNumber { get; set; }
        public string OrderType { get; set; }
        public string Currency { get; set; }
        public DateTime Created { get; set; }
        public DateTime? RequestedDelivery { get; set; }
        public string Status { get; set; } = "Open";
        public string DeliveryNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public XElement ShipTo { get; set; }
        public List<ErpSalesLine> Lines { get; } = new List<ErpSalesLine>();

        public decimal Value => Lines.Sum(l => l.Amount);
    }

    /// <summary>
    /// The IDoc side of the ERP (client VAPCLNT100): books ORDERS05 sales orders and INVOIC02 supplier invoices
    /// from erp/inbound, and writes DESADV01 (goods issue), INVOIC02 (billing) and ORDERS05 (purchase orders)
    /// to erp/outbound, where the BizTalk FILE receive location picks them up. Numbers follow the ERP's ranges.
    /// </summary>
    public class ErpSystem
    {
        private static readonly XNamespace IDocNs = MessageTypes.IDocNamespace;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly LabContext _lab;
        private readonly object _gate = new object();
        private long _nextOrder = 45001;
        private long _nextDelivery = 80012001;
        private long _nextInvoice = 90034001;
        private long _nextIDoc = 7310001;
        private long _nextHandlingUnit = 2600001;

        public ErpSystem(LabContext lab)
        {
            _lab = lab;
        }

        public List<ErpSalesOrder> Orders { get; } = new List<ErpSalesOrder>();
        public List<string> PostedSupplierInvoices { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public decimal OpenOrderValue(string soldTo)
        {
            lock (_gate)
            {
                return Orders.Where(o => o.SoldTo == soldTo.TrimStart('0') && o.Status != "Invoiced").Sum(o => o.Value);
            }
        }

        public ErpSalesOrder FindOrder(string soldTo, string customerReference, string erpOrderNumber)
        {
            lock (_gate)
            {
                return Orders.FirstOrDefault(o =>
                    (!string.IsNullOrEmpty(erpOrderNumber) && o.ErpOrderNumber.TrimStart('0') == erpOrderNumber.TrimStart('0'))
                    || (o.SoldTo == (soldTo ?? string.Empty).TrimStart('0') && o.CustomerReference == customerReference));
            }
        }

        /// <summary>Inbound IDoc processing (the ERP's file port). Returns the number of IDocs booked.</summary>
        public int ProcessInbound()
        {
            var folder = _lab.WorkPath("erp", "inbound");
            var done = Directory.CreateDirectory(Path.Combine(folder, "processed")).FullName;
            var count = 0;
            foreach (var file in Directory.GetFiles(folder, "*.xml").OrderBy(f => File.GetLastWriteTimeUtc(f)).ThenBy(f => f, StringComparer.Ordinal))
            {
                try
                {
                    var doc = XDocument.Load(file);
                    foreach (var idoc in doc.Root.Elements("IDOC"))
                    {
                        switch (doc.Root.Name.LocalName)
                        {
                            case "ORDERS05":
                                BookSalesOrder(idoc);
                                break;
                            case "INVOIC02":
                                PostSupplierInvoice(idoc);
                                break;
                            default:
                                throw new InvalidOperationException("No inbound process for " + doc.Root.Name.LocalName);
                        }

                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    _lab.Trace("ERP: IDoc {0} in status 51: {1}", Path.GetFileName(file), ex.Message);
                }

                File.Move(file, Path.Combine(done, Path.GetFileName(file)), true);
            }

            return count;
        }

        private void BookSalesOrder(XElement idoc)
        {
            var soldTo = Text(idoc.Elements("E1EDKA1").FirstOrDefault(p => Text(p, "PARVW") == "AG"), "PARTN").TrimStart('0');
            var reference = Text(idoc.Elements("E1EDK02").FirstOrDefault(r => Text(r, "QUALF") == "001"), "BELNR");
            if (string.IsNullOrEmpty(reference))
            {
                reference = Text(idoc.Element("E1EDK01"), "BELNR");
            }

            lock (_gate)
            {
                if (Orders.Any(o => o.SoldTo == soldTo && o.CustomerReference == reference))
                {
                    throw new InvalidOperationException("Customer reference " + reference + " already exists for sold-to " + soldTo);
                }
            }

            if (!_lab.Seed.ErpAccounts.Any(a => a.ErpNumber == soldTo))
            {
                throw new InvalidOperationException("Sold-to party " + soldTo + " does not exist");
            }

            var order = new ErpSalesOrder
            {
                SoldTo = soldTo,
                CustomerReference = reference,
                OrderType = Text(idoc.Element("E1EDK01"), "BSART"),
                Currency = Text(idoc.Element("E1EDK01"), "CURCY"),
                Created = _lab.Now,
                ShipTo = idoc.Elements("E1EDKA1").FirstOrDefault(p => Text(p, "PARVW") == "WE")
            };

            var requested = Text(idoc.Elements("E1EDK03").FirstOrDefault(d => Text(d, "IDDAT") == "002"), "DATUM");
            if (DateTime.TryParseExact(requested, "yyyyMMdd", Inv, DateTimeStyles.None, out var rdd))
            {
                order.RequestedDelivery = rdd;
            }

            foreach (var p in idoc.Elements("E1EDP01"))
            {
                var item = Ident(p, "002");
                var master = _lab.Seed.Item(item) ?? throw new InvalidOperationException("Material " + item + " does not exist");
                var price = decimal.TryParse(Text(p, "VPREI"), NumberStyles.Number, Inv, out var vprei) && vprei > 0 ? vprei : master.Price;
                order.Lines.Add(new ErpSalesLine
                {
                    Position = int.Parse(Text(p, "POSEX"), Inv),
                    Item = master.Item,
                    PartnerItem = Ident(p, "001"),
                    Gtin = Ident(p, "003") ?? master.Gtin,
                    Description = master.Description,
                    Quantity = decimal.Parse(Text(p, "MENGE"), Inv),
                    Uom = Text(p, "MENEE"),
                    Price = price
                });
            }

            lock (_gate)
            {
                order.ErpOrderNumber = (_nextOrder++).ToString("0000000000", Inv);
                Orders.Add(order);
            }

            _lab.Trace("ERP: sales order {0} for sold-to {1}, customer reference {2}, {3} line(s), {4:0.00} {5}", order.ErpOrderNumber.TrimStart('0'), soldTo, reference, order.Lines.Count, order.Value, order.Currency);
        }

        private void PostSupplierInvoice(XElement idoc)
        {
            var vendor = Text(idoc.Elements("E1EDKA1").FirstOrDefault(p => Text(p, "PARVW") == "LF"), "PARTN");
            var number = Text(idoc.Element("E1EDK01"), "BELNR");
            var po = Text(idoc.Elements("E1EDK02").FirstOrDefault(r => Text(r, "QUALF") == "001"), "BELNR");
            var known = _lab.Seed.PurchaseOrders.FirstOrDefault(p => p.PoNumber == po);
            if (known == null)
            {
                throw new InvalidOperationException("Purchase order " + po + " not found for invoice " + number);
            }

            if (known.Vendor != vendor.TrimStart('0'))
            {
                throw new InvalidOperationException("Invoice " + number + " vendor " + vendor + " does not match purchase order vendor " + known.Vendor);
            }

            var total = Text(idoc.Elements("E1EDS01").FirstOrDefault(s => Text(s, "SUMID") == "011"), "SUMME");
            lock (_gate)
            {
                PostedSupplierInvoices.Add(vendor + "/" + number);
            }

            _lab.Trace("ERP: supplier invoice {0} from vendor {1} parked against purchase order {2} ({3} {4})", number, vendor, po, total, Text(idoc.Element("E1EDK01"), "CURCY"));
        }

        /// <summary>Goods issue for open orders: outbound delivery and, for customers with EDI output, a DESADV01.</summary>
        public IList<string> GoodsIssue()
        {
            var files = new List<string>();
            foreach (var order in Snapshot("Open"))
            {
                order.DeliveryNumber = Next(ref _nextDelivery).ToString("0000000000", Inv);
                order.Status = "Delivered";
                var account = _lab.Seed.ErpAccounts.First(a => a.ErpNumber == order.SoldTo);
                if (!account.EdiOutput)
                {
                    _lab.Trace("ERP: delivery {0} for order {1} (no EDI output for {2})", order.DeliveryNumber.TrimStart('0'), order.ErpOrderNumber.TrimStart('0'), account.Name);
                    continue;
                }

                files.Add(Write("DESADV01", order.DeliveryNumber, Desadv(order)));
            }

            return files;
        }

        /// <summary>Billing run for delivered orders: INVOIC02 for customers with EDI output.</summary>
        public IList<string> Billing()
        {
            var files = new List<string>();
            foreach (var order in Snapshot("Delivered"))
            {
                order.InvoiceNumber = Next(ref _nextInvoice).ToString("0000000000", Inv);
                order.Status = "Invoiced";
                if (_lab.Seed.ErpAccounts.First(a => a.ErpNumber == order.SoldTo).EdiOutput)
                {
                    files.Add(Write("INVOIC02", order.InvoiceNumber, Invoice(order)));
                }
            }

            return files;
        }

        /// <summary>Output of the open purchase orders (message type ORDERS, partner type LI).</summary>
        public IList<string> ReleasePurchaseOrders()
        {
            return _lab.Seed.PurchaseOrders.Select(po => Write("ORDERS05", po.PoNumber, PurchaseOrder(po))).ToList();
        }

        private List<ErpSalesOrder> Snapshot(string status)
        {
            lock (_gate)
            {
                return Orders.Where(o => o.Status == status).ToList();
            }
        }

        private long Next(ref long counter)
        {
            lock (_gate)
            {
                return counter++;
            }
        }

        private XElement Desadv(ErpSalesOrder order)
        {
            var partner = _lab.Directory.FindByErpNumber(PartnerRole.Customer, order.SoldTo);
            var route = partner == null ? "STD" : _lab.Directory.Translate(partner.PartnerId, CrossReferenceKind.Carrier, partner.DefaultShipVia, _lab.Now.Date) ?? "STD";
            var account = _lab.Seed.ErpAccounts.First(a => a.ErpNumber == order.SoldTo);
            var weight = order.Lines.Sum(l => l.Quantity * (l.Uom == "M" ? 0.12m : 0.35m));
            var units = order.Lines.Select((l, i) => new { Line = l, Unit = i / 2 }).GroupBy(x => x.Unit).ToList();
            var handlingUnits = units.Select(g => new XElement("E1EDL37", Seg(),
                new XElement("EXIDV", Sscc.Create(0, Next(ref _nextHandlingUnit))),
                new XElement("VHART", "PAL"),
                g.Select(x => new XElement("E1EDL44", Seg(), new XElement("POSNR", Posnr(x.Line)), new XElement("VEMNG", Qty(x.Line.Quantity)))))).ToList();

            var header = new XElement("E1EDL20", Seg(),
                new XElement("VBELN", order.DeliveryNumber),
                new XElement("BTGEW", weight.ToString("0.000", Inv)),
                new XElement("GEWEI", "KGM"),
                new XElement("ANZPK", handlingUnits.Count.ToString(Inv)),
                new XElement("TRAID", Tracking(route, order.DeliveryNumber)),
                new XElement("ROUTE", route),
                new XElement("E1ADRM1", Seg(), new XElement("PARTNER_Q", "AG"), new XElement("PARTNER_ID", order.SoldTo.PadLeft(10, '0')), new XElement("NAME1", account.Name)),
                ShipToAddress(order.ShipTo),
                new XElement("E1EDT13", Seg(), new XElement("QUALF", "006"), new XElement("NTANF", _lab.Now.ToString("yyyyMMdd", Inv))),
                new XElement("E1EDT13", Seg(), new XElement("QUALF", "007"), new XElement("NTANF", _lab.Now.AddDays(2).ToString("yyyyMMdd", Inv))),
                order.Lines.Select(l => new XElement("E1EDL24", Seg(),
                    new XElement("POSNR", Posnr(l)),
                    new XElement("MATNR", l.Item),
                    new XElement("ARKTX", l.Description),
                    Optional("KDMAT", l.PartnerItem),
                    Optional("EAN11", l.Gtin),
                    new XElement("LFIMG", Qty(l.Quantity)),
                    new XElement("VRKME", l.Uom),
                    new XElement("VGBEL", order.ErpOrderNumber),
                    new XElement("E1EDL41", Seg(), new XElement("QUALI", "001"), new XElement("BSTNR", order.CustomerReference), new XElement("POSEX", l.Position.ToString("000000", Inv))))),
                handlingUnits);

            return Envelope("DESADV01", "DESADV", "KU", order.SoldTo, order.DeliveryNumber, header);
        }

        private XElement Invoice(ErpSalesOrder order)
        {
            var partner = _lab.Directory.FindByErpNumber(PartnerRole.Customer, order.SoldTo);
            var country = partner?.CountryCode ?? "US";
            var rate = country == "DE" ? 19m : country == "NL" ? 21m : 0m;
            var taxCode = rate > 0 ? "A1" : (country == "US" ? "U0" : "A0");
            var account = _lab.Seed.ErpAccounts.First(a => a.ErpNumber == order.SoldTo);
            var lineTotal = order.Value;
            var tax = order.Lines.Sum(l => Math.Round(l.Amount * rate / 100m, 2));
            var segments = new List<object>
            {
                new XElement("E1EDK01", Seg(), new XElement("CURCY", order.Currency), new XElement("ZTERM", "Z030"), new XElement("BSART", "INVO"), new XElement("BELNR", order.InvoiceNumber)),
                Party("AG", order.SoldTo.PadLeft(10, '0'), partner?.Gln, account.Name),
                Party("RE", order.SoldTo.PadLeft(10, '0'), partner?.Gln, account.Name),
                order.ShipTo == null ? null : new XElement("E1EDKA1", Seg(), order.ShipTo.Elements().Where(e => e.Name.LocalName != "IHREZ")),
                Party("RS", "VA01", "0614141000005", "VanArsdel Distribution"),
                new XElement("E1EDK02", Seg(), new XElement("QUALF", "001"), new XElement("BELNR", order.CustomerReference)),
                new XElement("E1EDK02", Seg(), new XElement("QUALF", "002"), new XElement("BELNR", order.ErpOrderNumber)),
                new XElement("E1EDK02", Seg(), new XElement("QUALF", "012"), new XElement("BELNR", order.DeliveryNumber)),
                new XElement("E1EDK03", Seg(), new XElement("IDDAT", "012"), new XElement("DATUM", _lab.Now.ToString("yyyyMMdd", Inv)))
            };

            segments.AddRange(order.Lines.Select(l => new XElement("E1EDP01", Seg(),
                new XElement("POSEX", l.Position.ToString("000000", Inv)),
                new XElement("MENGE", Qty(l.Quantity)),
                new XElement("MENEE", l.Uom),
                new XElement("PEINH", "1"),
                new XElement("E1EDP02", Seg(), new XElement("QUALF", "001"), new XElement("BELNR", l.Position.ToString("000000", Inv))),
                new XElement("E1EDP19", Seg(), new XElement("QUALF", "002"), new XElement("IDTNR", l.Item), new XElement("KTEXT", l.Description)),
                string.IsNullOrEmpty(l.PartnerItem) ? null : new XElement("E1EDP19", Seg(), new XElement("QUALF", "001"), new XElement("IDTNR", l.PartnerItem)),
                string.IsNullOrEmpty(l.Gtin) ? null : new XElement("E1EDP19", Seg(), new XElement("QUALF", "003"), new XElement("IDTNR", l.Gtin)),
                new XElement("E1EDP26", Seg(), new XElement("QUALF", "001"), new XElement("BETRG", Sap(l.Price))),
                new XElement("E1EDP26", Seg(), new XElement("QUALF", "003"), new XElement("BETRG", Sap(l.Amount))),
                new XElement("E1EDP04", Seg(), new XElement("MWSKZ", taxCode), new XElement("MSATZ", rate.ToString("0.000", Inv)), new XElement("MWSBT", Sap(Math.Round(l.Amount * rate / 100m, 2)))))));

            segments.Add(Summary("010", lineTotal, order.Currency));
            segments.Add(Summary("005", tax, order.Currency));
            segments.Add(Summary("011", lineTotal + tax, order.Currency));
            return Envelope("INVOIC02", "INVOIC", "KU", order.SoldTo, order.InvoiceNumber, segments.ToArray());
        }

        private XElement PurchaseOrder(ErpPurchaseOrder po)
        {
            var segments = new List<object>
            {
                new XElement("E1EDK01", Seg(), new XElement("CURCY", po.Currency), new XElement("BSART", "NB"), new XElement("BELNR", po.PoNumber)),
                new XElement("E1EDK03", Seg(), new XElement("IDDAT", "012"), new XElement("DATUM", po.OrderDate.ToString("yyyyMMdd", Inv))),
                new XElement("E1EDK03", Seg(), new XElement("IDDAT", "002"), new XElement("DATUM", po.DeliveryDate.ToString("yyyyMMdd", Inv))),
                new XElement("E1EDKA1", Seg(), new XElement("PARVW", "LF"), new XElement("PARTN", po.Vendor.PadLeft(10, '0'))),
                new XElement("E1EDKA1", Seg(), new XElement("PARVW", "WE"), new XElement("PARTN", "VA01"), new XElement("ILNNR", "0614141000029"),
                    new XElement("NAME1", "VanArsdel Distribution DC"), new XElement("STRAS", "Celsiusweg 41"), new XElement("ORT01", "Venlo"), new XElement("PSTLZ", "5928 PR"), new XElement("LAND1", "NL"))
            };

            segments.AddRange(po.Lines.Select(l =>
            {
                var item = _lab.Seed.Item(l.Item);
                return new XElement("E1EDP01", Seg(),
                    new XElement("POSEX", l.Line.ToString("00000", Inv)),
                    new XElement("MENGE", Qty(l.Quantity)),
                    new XElement("MENEE", l.Uom),
                    new XElement("VPREI", l.Price.ToString("0.00", Inv)),
                    new XElement("PEINH", "1"),
                    new XElement("E1EDP20", Seg(), new XElement("WMENG", Qty(l.Quantity)), new XElement("EDATU", po.DeliveryDate.ToString("yyyyMMdd", Inv))),
                    new XElement("E1EDP19", Seg(), new XElement("QUALF", "002"), new XElement("IDTNR", l.Item), item == null ? null : new XElement("KTEXT", item.Description)));
            }));

            segments.Add(Summary("001", po.Lines.Count, null));
            return Envelope("ORDERS05", "ORDERS", "LI", po.Vendor.PadLeft(10, '0'), po.PoNumber, segments.ToArray());
        }

        private XElement Envelope(string idocType, string messageType, string partnerType, string partnerNumber, string reference, params object[] content)
        {
            var control = new XElement("EDI_DC40", Seg(),
                new XElement("TABNAM", "EDI_DC40"),
                new XElement("MANDT", "100"),
                new XElement("DOCNUM", Next(ref _nextIDoc).ToString("0000000000000000", Inv)),
                new XElement("DIRECT", "1"),
                new XElement("IDOCTYP", idocType),
                new XElement("MESTYP", messageType),
                new XElement("SNDPOR", "SAPVAP"),
                new XElement("SNDPRT", "LS"),
                new XElement("SNDPRN", "VAPCLNT100"),
                new XElement("RCVPOR", "BIZTALK"),
                new XElement("RCVPRT", partnerType),
                new XElement("RCVPRN", partnerNumber),
                new XElement("CREDAT", _lab.Now.ToString("yyyyMMdd", Inv)),
                new XElement("CRETIM", _lab.Now.ToString("HHmmss", Inv)),
                new XElement("REFINT", reference.Length > 14 ? reference.Substring(0, 14) : reference));
            return new XElement(IDocNs + idocType, new XAttribute(XNamespace.Xmlns + "ns0", IDocNs.NamespaceName),
                new XElement("IDOC", new XAttribute("BEGIN", "1"), control, content));
        }

        private string Write(string idocType, string reference, XElement idoc)
        {
            var file = _lab.WorkPath("erp", "outbound", idocType + "_" + reference.TrimStart('0') + "_" + _lab.Now.ToString("yyyyMMddHHmmss", Inv) + ".xml");
            using (var writer = System.Xml.XmlWriter.Create(file, new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            {
                new XDocument(idoc).Save(writer);
            }

            _lab.Trace("ERP: {0} {1} written to the outbound IDoc port", idocType, reference.TrimStart('0'));
            return file;
        }

        private static XElement ShipToAddress(XElement shipTo)
        {
            if (shipTo == null)
            {
                return null;
            }

            return new XElement("E1ADRM1", Seg(),
                new XElement("PARTNER_Q", "WE"),
                new XElement("PARTNER_ID", Text(shipTo, "PARTN")),
                Optional("NAME1", Text(shipTo, "NAME1")),
                Optional("STREET1", Text(shipTo, "STRAS")),
                Optional("CITY1", Text(shipTo, "ORT01")),
                Optional("POSTL_COD1", Text(shipTo, "PSTLZ")),
                Optional("REGION", Text(shipTo, "REGIO")),
                Optional("COUNTRY1", Text(shipTo, "LAND1")));
        }

        private static XElement Party(string function, string number, string gln, string name)
        {
            return new XElement("E1EDKA1", Seg(), new XElement("PARVW", function), new XElement("PARTN", number), Optional("ILNNR", gln), Optional("NAME1", name));
        }

        private static XElement Summary(string id, decimal amount, string currency)
        {
            return new XElement("E1EDS01", Seg(), new XElement("SUMID", id), new XElement("SUMME", Sap(amount)), Optional("WAERQ", currency));
        }

        private static string Tracking(string route, string delivery)
        {
            switch (route)
            {
                case "UPSN":
                    return "1Z9V44A0" + delivery.Substring(delivery.Length - 8);
                case "FDEG":
                    return "7712" + delivery.Substring(delivery.Length - 8);
                default:
                    return "VA" + delivery.TrimStart('0');
            }
        }

        private static string Posnr(ErpSalesLine line) => (line.Position * 10).ToString("000000", Inv);

        private static string Qty(decimal q) => q.ToString("0.###", Inv);

        /// <summary>SAP amount format: trailing minus for negative values.</summary>
        private static string Sap(decimal amount) => amount < 0 ? (-amount).ToString("0.00", Inv) + "-" : amount.ToString("0.00", Inv);

        private static XAttribute Seg() => new XAttribute("SEGMENT", "1");

        private static XElement Optional(string name, string value) => string.IsNullOrEmpty(value) ? null : new XElement(name, value);

        private static string Ident(XElement position, string qualifier)
        {
            return Text(position.Elements("E1EDP19").FirstOrDefault(i => Text(i, "QUALF") == qualifier), "IDTNR");
        }

        private static string Text(XElement parent, string name)
        {
            return parent?.Element(name)?.Value;
        }
    }
}
