using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.AxIntegration.QueryService
{
    /// <summary>
    /// Bulk reads through the AX Query Service. Static queries (TSDealer*) are AOT queries delivered with the TSDealer
    /// model (see ax\Queries); the changed-orders read builds its query at run time to put a range on ModifiedDateTime.
    /// Result data sets have one table per data source, named after the data source.
    /// </summary>
    public class AxQueryReader : IAxQueryReader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AxQueryReader));
        private readonly AxChannels _channels;

        public AxQueryReader(AxChannels channels)
        {
            _channels = channels;
        }

        private DataSet Static(string query, int start, int size, out int? next)
        {
            var paging = new PositionBasedPaging { StartingPosition = start, NumberOfRecordsToFetch = size };
            var response = _channels.Call<IQueryService, ExecuteStaticQueryResponse>(_channels.QueryServiceAddress,
                s => s.ExecuteStaticQuery(new ExecuteStaticQueryRequest { queryName = query, paging = paging }), "QueryService " + query);
            var data = response.ExecuteStaticQueryResult ?? new DataSet();
            var rows = data.Tables.Count == 0 ? 0 : data.Tables[0].Rows.Count;
            next = rows < size ? (int?)null : start + size;
            Log.DebugFormat("{0}: {1} rows from position {2}", query, rows, start);
            return data;
        }

        private DataSet Dynamic(QueryMetadata query, int start, int size, out int? next)
        {
            var paging = new PositionBasedPaging { StartingPosition = start, NumberOfRecordsToFetch = size };
            var response = _channels.Call<IQueryService, ExecuteQueryResponse>(_channels.QueryServiceAddress,
                s => s.ExecuteQuery(new ExecuteQueryRequest { query = query, paging = paging }), "QueryService " + query.Name);
            var data = response.ExecuteQueryResult ?? new DataSet();
            var rows = data.Tables.Count == 0 ? 0 : data.Tables[0].Rows.Count;
            next = rows < size ? (int?)null : start + size;
            return data;
        }

        public AxPage<AxPriceRecord> ReadSalesPrices(int startPosition, int pageSize, AxCallContext context)
        {
            var data = Static("TSDealerSalesPrices", startPosition, pageSize, out var next);
            return new AxPage<AxPriceRecord>
            {
                NextPosition = next,
                Records = Rows(data, "PriceDiscTable").Select(r => new AxPriceRecord
                {
                    ItemRelation = Str(r, "ItemRelation"),
                    AccountRelation = Str(r, "AccountRelation"),
                    Currency = Str(r, "Currency"),
                    Amount = Dec(r, "Amount"),
                    QuantityAmountFrom = Dec(r, "QuantityAmountFrom"),
                    FromDate = Date(r, "FromDate") ?? DateTime.MinValue,
                    ToDate = Date(r, "ToDate")
                }).ToList()
            };
        }

        public AxPage<AxStockRecord> ReadAvailability(int startPosition, int pageSize, AxCallContext context)
        {
            var data = Static("TSDealerOnHand", startPosition, pageSize, out var next);
            var dims = Rows(data, "InventDim").ToDictionary(r => Str(r, "inventDimId"), StringComparer.OrdinalIgnoreCase);
            return new AxPage<AxStockRecord>
            {
                NextPosition = next,
                Records = Rows(data, "InventSum").Select(r =>
                {
                    dims.TryGetValue(Str(r, "InventDimId"), out var dim);
                    return new AxStockRecord
                    {
                        ItemId = Str(r, "ItemId"),
                        ConfigId = dim == null ? "" : Str(dim, "configId"),
                        InventLocationId = dim == null ? "" : Str(dim, "InventLocationId"),
                        PhysicalInvent = Dec(r, "PhysicalInvent"),
                        ReservPhysical = Dec(r, "ReservPhysical"),
                        OnOrder = Dec(r, "OnOrder")
                    };
                }).ToList()
            };
        }

        public AxPage<AxCustomerBalance> ReadCustomerBalances(int startPosition, int pageSize, AxCallContext context)
        {
            var data = Static("TSDealerCustBalance", startPosition, pageSize, out var next);
            return new AxPage<AxCustomerBalance>
            {
                NextPosition = next,
                Records = Rows(data, "TSDealerCustBalanceView").Select(r => new AxCustomerBalance
                {
                    AccountNum = Str(r, "AccountNum"),
                    Balance = Dec(r, "Balance"),
                    CreditMax = Dec(r, "CreditMax"),
                    Blocked = (AxCustomerBlocked)Convert.ToInt32(r["Blocked"], CultureInfo.InvariantCulture),
                    OpenOrderValue = Dec(r, "OpenOrderValue")
                }).ToList()
            };
        }

        public AxPage<AxOrderHeaderStatus> ReadChangedOrders(DateTime changedSinceUtc, int startPosition, int pageSize, AxCallContext context)
        {
            var query = new QueryMetadata { Name = "TSDealerOrderChanges" };
            var sales = new QueryDataSourceMetadata { Name = "SalesTable", Table = "SalesTable" };
            sales.Ranges.Add(new QueryDataRangeMetadata { TableName = "SalesTable", FieldName = "SalesOriginId", Value = "PORTAL,EDI,DESK" });
            // AX date-time ranges take the UTC value in the yyyy-MM-ddTHH:mm:ss format followed by '..' for an open end.
            sales.Ranges.Add(new QueryDataRangeMetadata { TableName = "SalesTable", FieldName = "ModifiedDateTime", Value = changedSinceUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + ".." });
            query.DataSources.Add(sales);

            var data = Dynamic(query, startPosition, pageSize, out var next);
            return new AxPage<AxOrderHeaderStatus>
            {
                NextPosition = next,
                Records = Rows(data, "SalesTable").Select(r => new AxOrderHeaderStatus
                {
                    SalesId = Str(r, "SalesId"),
                    PortalReference = Str(r, "PurchOrderFormNum"),
                    SalesStatus = (AxSalesStatus)Convert.ToInt32(r["SalesStatus"], CultureInfo.InvariantCulture),
                    DocumentStatus = (AxDocumentStatus)Convert.ToInt32(r["DocumentStatus"], CultureInfo.InvariantCulture),
                    ModifiedDateTime = Date(r, "ModifiedDateTime") ?? DateTime.MinValue
                }).ToList()
            };
        }

        public AxPage<AxExchangeRateRecord> ReadExchangeRates(int startPosition, int pageSize, AxCallContext context)
        {
            var data = Static("TSDealerExchangeRates", startPosition, pageSize, out var next);
            var pairs = Rows(data, "ExchangeRateCurrencyPair").ToDictionary(r => Convert.ToInt64(r["RecId"], CultureInfo.InvariantCulture));
            return new AxPage<AxExchangeRateRecord>
            {
                NextPosition = next,
                Records = Rows(data, "ExchangeRate").Select(r =>
                {
                    var pair = pairs[Convert.ToInt64(r["ExchangeRateCurrencyPair"], CultureInfo.InvariantCulture)];
                    // AX stores rates per 100 units of the from currency.
                    return new AxExchangeRateRecord
                    {
                        FromCurrency = Str(pair, "FromCurrencyCode"),
                        ToCurrency = Str(pair, "ToCurrencyCode"),
                        Rate = Dec(r, "ExchangeRate") / 100m,
                        ValidFrom = Date(r, "ValidFrom") ?? DateTime.MinValue
                    };
                }).ToList()
            };
        }

        public AxPage<AxItemRecord> ReadItems(int startPosition, int pageSize, AxCallContext context)
        {
            var data = Static("TSDealerItems", startPosition, pageSize, out var next);
            return new AxPage<AxItemRecord>
            {
                NextPosition = next,
                Records = Rows(data, "InventTable").Select(r => new AxItemRecord
                {
                    ItemId = Str(r, "ItemId"),
                    Name = Str(r, "NameAlias"),
                    ItemGroup = Str(r, "TSDealerItemGroup"),
                    NetWeightKg = Dec(r, "NetWeight"),
                    Stopped = Convert.ToInt32(r["TSStopped"], CultureInfo.InvariantCulture) == 1
                }).ToList()
            };
        }

        /// <summary>Adds packing slips and invoices of one order (TSDealerShipments query with a SalesId range).</summary>
        internal void AddShipments(AxSalesOrderSnapshot snapshot, AxCallContext context)
        {
            var query = new QueryMetadata { Name = "TSDealerShipments" };
            var jour = new QueryDataSourceMetadata { Name = "CustPackingSlipJour", Table = "CustPackingSlipJour", FetchMode = FetchMode.OneToN };
            jour.Ranges.Add(new QueryDataRangeMetadata { TableName = "CustPackingSlipJour", FieldName = "SalesId", Value = snapshot.SalesId });
            var trans = new QueryDataSourceMetadata { Name = "CustPackingSlipTrans", Table = "CustPackingSlipTrans", JoinMode = JoinMode.OuterJoin };
            trans.Relations.Add(new QueryRelationMetadata { Table = "CustPackingSlipTrans", Field = "PackingSlipId", RelatedTable = "CustPackingSlipJour", RelatedField = "PackingSlipId" });
            jour.DataSources.Add(trans);
            query.DataSources.Add(jour);
            var invoices = new QueryDataSourceMetadata { Name = "CustInvoiceJour", Table = "CustInvoiceJour" };
            invoices.Ranges.Add(new QueryDataRangeMetadata { TableName = "CustInvoiceJour", FieldName = "SalesId", Value = snapshot.SalesId });
            query.DataSources.Add(invoices);

            var data = Dynamic(query, 1, 1000, out _);
            var lines = Rows(data, "CustPackingSlipTrans").ToList();
            foreach (var j in Rows(data, "CustPackingSlipJour"))
            {
                var id = Str(j, "PackingSlipId");
                var slip = new AxPackingSlip
                {
                    PackingSlipId = id,
                    DeliveryDate = Date(j, "DeliveryDate") ?? DateTime.MinValue,
                    CarrierCode = Str(j, "DlvModeId"),
                    TrackingNumber = Str(j, "TSTrackingNumber"),
                    TrailerNumber = Str(j, "TSTrailerNumber"),
                    GrossWeightKg = Dec(j, "Weight"),
                    Packages = (int)Dec(j, "Qty")
                };
                slip.Lines.AddRange(lines.Where(l => Str(l, "PackingSlipId") == id).Select(l => new AxPackingSlipLine
                {
                    InventTransId = Str(l, "InventTransId"),
                    ItemId = Str(l, "ItemId"),
                    Quantity = Dec(l, "Qty"),
                    SerialId = Str(l, "inventSerialId")
                }));
                snapshot.PackingSlips.Add(slip);
            }
            snapshot.InvoiceIds.AddRange(Rows(data, "CustInvoiceJour").Select(r => Str(r, "InvoiceId")).Distinct());
        }

        private static IEnumerable<DataRow> Rows(DataSet data, string table) =>
            data.Tables.Contains(table) ? data.Tables[table].Rows.Cast<DataRow>() : Enumerable.Empty<DataRow>();

        private static string Str(DataRow row, string column) =>
            row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToString(row[column], CultureInfo.InvariantCulture).Trim() : null;

        private static decimal Dec(DataRow row, string column) =>
            row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToDecimal(row[column], CultureInfo.InvariantCulture) : 0m;

        private static DateTime? Date(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            var value = Convert.ToDateTime(row[column], CultureInfo.InvariantCulture);
            // AX uses 1900-01-01 as the empty date.
            return value.Year <= 1900 ? (DateTime?)null : value;
        }
    }
}
