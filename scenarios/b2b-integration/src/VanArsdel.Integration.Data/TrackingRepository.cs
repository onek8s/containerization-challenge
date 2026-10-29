using System;
using System.Collections.Generic;
using System.Data;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Data
{
    public class OrderSearch
    {
        public string PartnerId { get; set; }
        public string OrderNumber { get; set; }
        public string ErpOrderNumber { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public string Status { get; set; }
        public int MaxRows { get; set; } = 500;
    }

    /// <summary>
    /// Reads the reporting views over the BAMPrimaryImport database (rpt.vOrderLifecycle unions the BAM
    /// bam_OrderLifecycle_Active and _Completed views). Used by the tracking portal, the gateway's order status
    /// operation and the SLA monitor. Connection "BAMPrimaryImport" (read-only login).
    /// </summary>
    public class TrackingRepository
    {
        private readonly IntegrationDatabase _db;

        public TrackingRepository(IntegrationDatabase bamDb)
        {
            _db = bamDb;
        }

        public IList<OrderActivityRow> SearchOrders(OrderSearch search)
        {
            using (var cmd = _db.Procedure("rpt.usp_SearchOrderLifecycle", 60))
            {
                _db.In(cmd, "@PartnerId", DbType.String, search.PartnerId);
                _db.In(cmd, "@OrderNumber", DbType.String, search.OrderNumber);
                _db.In(cmd, "@ErpOrderNumber", DbType.String, search.ErpOrderNumber);
                _db.In(cmd, "@FromUtc", DbType.DateTime2, search.FromUtc);
                _db.In(cmd, "@ToUtc", DbType.DateTime2, search.ToUtc);
                _db.In(cmd, "@Status", DbType.String, search.Status);
                _db.In(cmd, "@MaxRows", DbType.Int32, search.MaxRows);
                return Read(cmd);
            }
        }

        public IList<OrderActivityRow> OpenOrders()
        {
            using (var cmd = _db.Text("SELECT * FROM rpt.vOrderLifecycle WHERE IsCompleted = 0", 60))
            {
                return Read(cmd);
            }
        }

        public OrderActivityRow FindOrder(string partnerId, string orderNumber)
        {
            using (var cmd = _db.Text("SELECT TOP (1) * FROM rpt.vOrderLifecycle WHERE PartnerId = @PartnerId AND OrderNumber = @OrderNumber ORDER BY ReceivedUtc DESC"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                _db.In(cmd, "@OrderNumber", DbType.String, orderNumber);
                var rows = Read(cmd);
                return rows.Count == 0 ? null : rows[0];
            }
        }

        public IList<PartnerActivity> PartnerActivity()
        {
            var list = new List<PartnerActivity>();
            using (var cmd = _db.Text("SELECT PartnerId, LastReceivedUtc, ExpectsDailyTraffic FROM rpt.vPartnerLastReceived"))
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new PartnerActivity
                    {
                        PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                        LastReceivedUtc = IntegrationDatabase.D(r, "LastReceivedUtc"),
                        ExpectsDailyTraffic = IntegrationDatabase.B(r, "ExpectsDailyTraffic")
                    });
                }
            }

            return list;
        }

        public DataTable DailyVolumes(DateTime fromUtc, DateTime toUtc)
        {
            using (var cmd = _db.Procedure("rpt.usp_DailyDocumentVolumes", 120))
            {
                _db.In(cmd, "@FromUtc", DbType.DateTime2, fromUtc);
                _db.In(cmd, "@ToUtc", DbType.DateTime2, toUtc);
                var ds = _db.Database.ExecuteDataSet(cmd);
                return ds.Tables[0];
            }
        }

        private IList<OrderActivityRow> Read(System.Data.Common.DbCommand cmd)
        {
            var list = new List<OrderActivityRow>();
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new OrderActivityRow
                    {
                        ActivityId = IntegrationDatabase.S(r, "ActivityID"),
                        PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                        OrderNumber = IntegrationDatabase.S(r, "OrderNumber"),
                        ErpOrderNumber = IntegrationDatabase.S(r, "ErpOrderNumber"),
                        OrderValue = IntegrationDatabase.M(r, "OrderValue"),
                        LineCount = IntegrationDatabase.I(r, "LineCount"),
                        ReceivedUtc = IntegrationDatabase.D(r, "ReceivedUtc"),
                        ValidatedUtc = IntegrationDatabase.D(r, "ValidatedUtc"),
                        CreditHoldUtc = IntegrationDatabase.D(r, "CreditHoldUtc"),
                        ReleasedUtc = IntegrationDatabase.D(r, "ReleasedUtc"),
                        SentToErpUtc = IntegrationDatabase.D(r, "SentToErpUtc"),
                        ConfirmedUtc = IntegrationDatabase.D(r, "ConfirmedUtc"),
                        DespatchedUtc = IntegrationDatabase.D(r, "DespatchedUtc"),
                        AsnSentUtc = IntegrationDatabase.D(r, "AsnSentUtc"),
                        InvoicedUtc = IntegrationDatabase.D(r, "InvoicedUtc"),
                        RejectedUtc = IntegrationDatabase.D(r, "RejectedUtc"),
                        RejectReason = IntegrationDatabase.S(r, "RejectReason"),
                        HoldReason = IntegrationDatabase.S(r, "HoldReason"),
                        DespatchNumber = IntegrationDatabase.S(r, "DespatchNumber"),
                        InvoiceNumber = IntegrationDatabase.S(r, "InvoiceNumber")
                    });
                }
            }

            return list;
        }
    }
}
