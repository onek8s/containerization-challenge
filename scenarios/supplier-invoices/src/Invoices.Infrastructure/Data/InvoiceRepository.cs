using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Wingtip.Invoices.Core;
using Wingtip.Invoices.Core.Matching;

namespace Wingtip.Invoices.Infrastructure.Data
{
    public class StoredInvoice
    {
        public long InvoiceId { get; set; }
        public long FileId { get; set; }
        public int SupplierId { get; set; }
        public string VendorNumber { get; set; }
        public InvoiceStatus Status { get; set; }
        public InvoiceDocument Document { get; set; }
        public Dictionary<int, string> LineAccounts { get; set; }
    }

    public class SaveOutcome
    {
        public long InvoiceId { get; set; }

        /// <summary>Inserted, Replaced, Exists (same file processed before) or Duplicate (number used by another file).</summary>
        public string Outcome { get; set; }

        public InvoiceStatus Status { get; set; }
    }

    public class InvoiceRepository
    {
        public SupplierInfo GetSupplier(int supplierId)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.GetSupplier"))
            {
                cmd.Add("@SupplierId", SqlDbType.Int, supplierId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new SupplierInfo
                    {
                        SupplierId = supplierId,
                        Code = r.String("Code"),
                        Name = r.String("Name"),
                        VatNumber = r.String("VatNumber"),
                        VendorNumber = r.String("VendorNumber"),
                        CodePage = r.GetInt32(r.GetOrdinal("CodePage")),
                        PriceTolerancePercent = r.Nullable<decimal>("PriceTolerancePercent"),
                        TwoWayMatch = r.GetBoolean(r.GetOrdinal("TwoWayMatch")),
                        CostCenter = r.String("CostCenter")
                    };
                }
            }
        }

        public SaveOutcome Save(long fileId, int supplierId, string vendorNumber, InvoiceDocument invoice, MatchResult match, IList<Issue> validationIssues)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.SaveInvoice"))
            {
                cmd.Add("@FileId", SqlDbType.BigInt, fileId);
                cmd.Add("@SupplierId", SqlDbType.Int, supplierId);
                cmd.Add("@VendorNumber", SqlDbType.VarChar, vendorNumber, 10);
                cmd.Add("@InvoiceNumber", SqlDbType.NVarChar, invoice.InvoiceNumber, 40);
                cmd.Add("@DocumentType", SqlDbType.SmallInt, (short)invoice.Type);
                cmd.Add("@InvoiceDate", SqlDbType.Date, invoice.InvoiceDate);
                cmd.Add("@DueDate", SqlDbType.Date, invoice.DueDate);
                cmd.Add("@Currency", SqlDbType.Char, invoice.Currency, 3);
                cmd.Add("@NetAmount", SqlDbType.Decimal, invoice.NetAmount);
                cmd.Add("@VatAmount", SqlDbType.Decimal, invoice.VatAmount);
                cmd.Add("@GrossAmount", SqlDbType.Decimal, invoice.GrossAmount);
                cmd.Add("@PurchaseOrderNumber", SqlDbType.VarChar, invoice.PurchaseOrderNumber, 20);
                cmd.Add("@PaymentReference", SqlDbType.VarChar, invoice.PaymentReference, 35);
                cmd.Add("@SupplierVatNumber", SqlDbType.VarChar, invoice.SupplierVatNumber, 20);

                var issues = new List<Issue>(validationIssues);
                InvoiceStatus status;
                string summary;
                if (validationIssues.Count > 0)
                {
                    status = InvoiceStatus.Exception;
                    summary = "Validation failed";
                }
                else
                {
                    issues.AddRange(match.Issues);
                    status = match.Status;
                    summary = match.Summary;
                }
                cmd.Add("@Status", SqlDbType.TinyInt, (byte)status);
                cmd.Add("@MatchSummary", SqlDbType.NVarChar, FileRepository.Truncate(summary, 400), 400);
                cmd.Parameters.Add(LinesTable(invoice, match));
                cmd.Parameters.Add(IssuesTable(issues));

                var id = cmd.AddOutput("@InvoiceId", SqlDbType.BigInt);
                var outcome = cmd.AddOutput("@Outcome", SqlDbType.VarChar);
                outcome.Size = 10;
                var current = cmd.AddOutput("@CurrentStatus", SqlDbType.TinyInt);
                cmd.ExecuteNonQuery();

                return new SaveOutcome
                {
                    InvoiceId = (long)id.Value,
                    Outcome = (string)outcome.Value,
                    Status = (InvoiceStatus)(byte)current.Value
                };
            }
        }

        public void UpdateMatch(long invoiceId, MatchResult match, string user)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.UpdateMatch"))
            {
                cmd.Add("@InvoiceId", SqlDbType.BigInt, invoiceId);
                cmd.Add("@Status", SqlDbType.TinyInt, (byte)match.Status);
                cmd.Add("@MatchSummary", SqlDbType.NVarChar, FileRepository.Truncate(match.Summary, 400), 400);
                cmd.Add("@User", SqlDbType.NVarChar, user, 128);
                var accounts = new DataTable();
                accounts.Columns.Add("LineNumber", typeof(int));
                accounts.Columns.Add("Account", typeof(string));
                foreach (var l in match.Lines) accounts.Rows.Add(l.InvoiceLine, (object)l.Account ?? DBNull.Value);
                cmd.Parameters.Add(new SqlParameter("@Accounts", SqlDbType.Structured) { TypeName = "inv.LineAccountType", Value = accounts });
                cmd.Parameters.Add(IssuesTable(match.Issues));
                cmd.ExecuteNonQuery();
            }
        }

        public StoredInvoice Load(long invoiceId)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.GetInvoice"))
            {
                cmd.Add("@InvoiceId", SqlDbType.BigInt, invoiceId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    var doc = new InvoiceDocument
                    {
                        InvoiceNumber = r.String("InvoiceNumber"),
                        Type = (DocumentType)r.GetInt16(r.GetOrdinal("DocumentType")),
                        InvoiceDate = r.GetDateTime(r.GetOrdinal("InvoiceDate")),
                        DueDate = r.Nullable<DateTime>("DueDate"),
                        Currency = r.String("Currency"),
                        NetAmount = r.GetDecimal(r.GetOrdinal("NetAmount")),
                        VatAmount = r.GetDecimal(r.GetOrdinal("VatAmount")),
                        GrossAmount = r.GetDecimal(r.GetOrdinal("GrossAmount")),
                        PurchaseOrderNumber = r.String("PurchaseOrderNumber"),
                        PaymentReference = r.String("PaymentReference"),
                        SupplierVatNumber = r.String("SupplierVatNumber")
                    };
                    var stored = new StoredInvoice
                    {
                        InvoiceId = invoiceId,
                        FileId = r.GetInt64(r.GetOrdinal("FileId")),
                        SupplierId = r.GetInt32(r.GetOrdinal("SupplierId")),
                        VendorNumber = r.String("VendorNumber"),
                        Status = (InvoiceStatus)r.GetByte(r.GetOrdinal("Status")),
                        Document = doc,
                        LineAccounts = new Dictionary<int, string>()
                    };

                    r.NextResult();
                    while (r.Read())
                    {
                        var line = new InvoiceLine
                        {
                            LineNumber = r.GetInt32(r.GetOrdinal("LineNumber")),
                            ItemNumber = r.String("ItemNumber"),
                            SupplierItemNumber = r.String("SupplierItemNumber"),
                            Description = r.String("Description"),
                            Quantity = r.GetDecimal(r.GetOrdinal("Quantity")),
                            Unit = r.String("Unit"),
                            UnitPrice = r.GetDecimal(r.GetOrdinal("UnitPrice")),
                            NetAmount = r.GetDecimal(r.GetOrdinal("NetAmount")),
                            VatRate = r.GetDecimal(r.GetOrdinal("VatRate")),
                            PurchaseOrderNumber = r.String("PurchaseOrderNumber"),
                            PurchaseOrderLine = r.Nullable<int>("PurchaseOrderLine")
                        };
                        doc.Lines.Add(line);
                        stored.LineAccounts[line.LineNumber] = r.String("Account");
                    }
                    return stored;
                }
            }
        }

        /// <summary>
        /// Quantities on invoices that are in the hub but not yet posted in the ERP, per order line.
        /// </summary>
        public Dictionary<int, decimal> GetPendingQuantities(string orderNumber, int supplierId, string invoiceNumber)
        {
            var result = new Dictionary<int, decimal>();
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.GetPendingOrderQuantities"))
            {
                cmd.Add("@PurchaseOrderNumber", SqlDbType.VarChar, orderNumber, 20);
                cmd.Add("@SupplierId", SqlDbType.Int, supplierId);
                cmd.Add("@InvoiceNumber", SqlDbType.NVarChar, invoiceNumber, 40);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) result[r.GetInt32(0)] = r.GetDecimal(1);
                }
            }
            return result;
        }

        /// <summary>Moves Matched or Exception invoices to Released. Returns false when the status changed meanwhile.</summary>
        public bool MarkReleased(long invoiceId, string user, string comment)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.ReleaseInvoice"))
            {
                cmd.Add("@InvoiceId", SqlDbType.BigInt, invoiceId);
                cmd.Add("@User", SqlDbType.NVarChar, user, 128);
                cmd.Add("@Comment", SqlDbType.NVarChar, comment, 1000);
                var ok = cmd.AddOutput("@Released", SqlDbType.Bit);
                cmd.ExecuteNonQuery();
                return (bool)ok.Value;
            }
        }

        public void AddHistory(long invoiceId, string user, string action, string comment)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.AddInvoiceHistory"))
            {
                cmd.Add("@InvoiceId", SqlDbType.BigInt, invoiceId);
                cmd.Add("@User", SqlDbType.NVarChar, user, 128);
                cmd.Add("@Action", SqlDbType.VarChar, action, 30);
                cmd.Add("@Comment", SqlDbType.NVarChar, FileRepository.Truncate(comment, 1000), 1000);
                cmd.ExecuteNonQuery();
            }
        }

        private static SqlParameter LinesTable(InvoiceDocument invoice, MatchResult match)
        {
            var accounts = new Dictionary<int, string>();
            if (match != null)
            {
                foreach (var l in match.Lines) accounts[l.InvoiceLine] = l.Account;
            }

            var t = new DataTable();
            t.Columns.Add("LineNumber", typeof(int));
            t.Columns.Add("ItemNumber", typeof(string));
            t.Columns.Add("SupplierItemNumber", typeof(string));
            t.Columns.Add("Description", typeof(string));
            t.Columns.Add("Quantity", typeof(decimal));
            t.Columns.Add("Unit", typeof(string));
            t.Columns.Add("UnitPrice", typeof(decimal));
            t.Columns.Add("NetAmount", typeof(decimal));
            t.Columns.Add("VatRate", typeof(decimal));
            t.Columns.Add("PurchaseOrderNumber", typeof(string));
            t.Columns.Add("PurchaseOrderLine", typeof(int));
            t.Columns.Add("Account", typeof(string));
            foreach (var l in invoice.Lines)
            {
                string account;
                accounts.TryGetValue(l.LineNumber, out account);
                t.Rows.Add(l.LineNumber, (object)l.ItemNumber ?? DBNull.Value, (object)l.SupplierItemNumber ?? DBNull.Value,
                    (object)FileRepository.Truncate(l.Description, 200) ?? DBNull.Value, l.Quantity, (object)l.Unit ?? DBNull.Value,
                    l.UnitPrice, l.NetAmount, l.VatRate, (object)l.PurchaseOrderNumber ?? DBNull.Value,
                    (object)l.PurchaseOrderLine ?? DBNull.Value, (object)account ?? DBNull.Value);
            }
            return new SqlParameter("@Lines", SqlDbType.Structured) { TypeName = "inv.InvoiceLineType", Value = t };
        }

        private static SqlParameter IssuesTable(IEnumerable<Issue> issues)
        {
            var t = new DataTable();
            t.Columns.Add("Code", typeof(string));
            t.Columns.Add("Message", typeof(string));
            foreach (var i in issues) t.Rows.Add(i.Code, FileRepository.Truncate(i.Message, 400));
            return new SqlParameter("@Issues", SqlDbType.Structured) { TypeName = "inv.IssueType", Value = t };
        }
    }
}
