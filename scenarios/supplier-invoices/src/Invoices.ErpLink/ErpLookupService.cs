using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.Caching;
using Wingtip.Invoices.Contracts.Erp;
using Wingtip.Invoices.Core;
using Wingtip.Invoices.Core.Matching;
using Wingtip.Invoices.Infrastructure.Configuration;

namespace Wingtip.Invoices.ErpLink
{
    /// <summary>
    /// Published as a remoting singleton (ErpLookup.rem). Reads the ERP database with the read-only
    /// reporting login; vendors are cached for ten minutes.
    /// </summary>
    public class ErpLookupService : MarshalByRefObject, IErpLookup
    {
        private static readonly MemoryCache Cache = new MemoryCache("ErpLookup");

        public override object InitializeLifetimeService()
        {
            // Lives as long as the service.
            return null;
        }

        public Vendor FindVendorByVatNumber(string vatNumber)
        {
            var normalized = VatNumber.Normalize(vatNumber);
            if (normalized == null) return null;
            return Cached("vat:" + normalized, () => ReadVendor(
                "SELECT VendorNo, Name, VatRegNo, CurrencyCode, PaymentTerms, Blocked FROM erp.Vendor WHERE VatRegNoNormalized = @Key AND Company = @Company",
                normalized));
        }

        public Vendor GetVendor(string vendorNumber)
        {
            if (string.IsNullOrWhiteSpace(vendorNumber)) return null;
            return Cached("no:" + vendorNumber.Trim(), () => ReadVendor(
                "SELECT VendorNo, Name, VatRegNo, CurrencyCode, PaymentTerms, Blocked FROM erp.Vendor WHERE VendorNo = @Key AND Company = @Company",
                vendorNumber.Trim()));
        }

        public PurchaseOrder GetPurchaseOrder(string orderNumber)
        {
            using (var cn = Open())
            {
                PurchaseOrder order;
                using (var cmd = new SqlCommand(
                    "SELECT PoNo, VendorNo, CurrencyCode, BuyerId, CostCenter, Status FROM erp.PurchaseOrder WHERE PoNo = @PoNo AND Company = @Company", cn))
                {
                    cmd.Parameters.Add("@PoNo", SqlDbType.VarChar, 10).Value = orderNumber;
                    cmd.Parameters.Add("@Company", SqlDbType.Char, 4).Value = HubSettings.Company;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return null;
                        order = new PurchaseOrder
                        {
                            Number = r.GetString(0).Trim(),
                            VendorNumber = r.GetString(1).Trim(),
                            Currency = r.GetString(2),
                            Buyer = r.IsDBNull(3) ? null : r.GetString(3),
                            CostCenter = r.IsDBNull(4) ? null : r.GetString(4),
                            Closed = r.GetString(5) == "C"
                        };
                    }
                }

                using (var cmd = new SqlCommand(
                    @"SELECT l.[LineNo], l.ItemNo, l.VendorItemNo, l.Description, l.OrderedQty, l.ReceivedQty, l.InvoicedQty, l.UnitPrice / NULLIF(l.PriceUnit, 0), l.GlAccount, l.LineType
                      FROM erp.PurchaseOrderLine l WHERE l.PoNo = @PoNo AND l.Company = @Company AND l.Cancelled = 0 ORDER BY l.[LineNo]", cn))
                {
                    cmd.Parameters.Add("@PoNo", SqlDbType.VarChar, 10).Value = orderNumber;
                    cmd.Parameters.Add("@Company", SqlDbType.Char, 4).Value = HubSettings.Company;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            order.Lines.Add(new PurchaseOrderLine
                            {
                                LineNumber = r.GetInt32(0),
                                ItemNumber = r.IsDBNull(1) ? null : r.GetString(1).Trim(),
                                SupplierItemNumber = r.IsDBNull(2) ? null : r.GetString(2).Trim(),
                                Description = r.IsDBNull(3) ? null : r.GetString(3),
                                OrderedQuantity = r.GetDecimal(4),
                                ReceivedQuantity = r.GetDecimal(5),
                                InvoicedQuantity = r.GetDecimal(6),
                                UnitPrice = r.IsDBNull(7) ? 0m : Math.Round(r.GetDecimal(7), 4),
                                Account = r.IsDBNull(8) ? null : r.GetString(8).Trim(),
                                ReceiptRequired = r.GetString(9) == "I"
                            });
                        }
                    }
                }
                return order;
            }
        }

        public DateTime GetOpenPeriodStart()
        {
            return Cached("period", () =>
            {
                using (var cn = Open())
                using (var cmd = new SqlCommand("SELECT MIN(PeriodStart) FROM erp.ApPeriod WHERE Company = @Company AND Closed = 0", cn))
                {
                    cmd.Parameters.Add("@Company", SqlDbType.Char, 4).Value = HubSettings.Company;
                    var value = cmd.ExecuteScalar();
                    return value == DBNull.Value ? (object)new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : value;
                }
            }) as DateTime? ?? DateTime.Today;
        }

        public string Ping()
        {
            using (var cn = Open())
            {
                return Environment.MachineName + " ERP " + cn.DataSource + "/" + cn.Database;
            }
        }

        private static Vendor ReadVendor(string sql, string key)
        {
            using (var cn = Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@Key", SqlDbType.VarChar, 20).Value = key;
                cmd.Parameters.Add("@Company", SqlDbType.Char, 4).Value = HubSettings.Company;
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new Vendor
                    {
                        Number = r.GetString(0).Trim(),
                        Name = r.GetString(1),
                        VatNumber = r.IsDBNull(2) ? null : r.GetString(2),
                        Currency = r.GetString(3),
                        PaymentTerms = r.IsDBNull(4) ? null : r.GetString(4),
                        Blocked = r.GetBoolean(5)
                    };
                }
            }
        }

        private static T Cached<T>(string key, Func<T> load) where T : class
        {
            var value = Cache.Get(key) as T;
            if (value != null) return value;
            value = load();
            if (value != null) Cache.Set(key, value, DateTimeOffset.Now.AddMinutes(10));
            return value;
        }

        private static SqlConnection Open()
        {
            var cs = ConfigurationManager.ConnectionStrings["Erp"];
            if (cs == null) throw new ConfigurationErrorsException("Connection string 'Erp' is missing.");
            var cn = new SqlConnection(cs.ConnectionString);
            cn.Open();
            return cn;
        }
    }
}
