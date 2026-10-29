using System.Globalization;
using VanArsdel.Integration.Contracts.Erp;
using VanArsdel.Integration.LabStubs.Seed;

namespace VanArsdel.Integration.LabStubs.Simulators
{
    /// <summary>
    /// Stand-in for the ERP order management service on erp01 (SOAP 1.1 over basicHttp), hosted with CoreWCF.
    /// Credit data comes from samples/seed/erp-accounts.json; exposure grows with every order the IDoc side books.
    /// </summary>
    [CoreWCF.ServiceBehavior(InstanceContextMode = CoreWCF.InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
    public class ErpOrderService : IOrderManagementService
    {
        private readonly SeedData _seed;
        private readonly ErpSystem _erp;
        private readonly object _gate = new object();

        public ErpOrderService(SeedData seed, ErpSystem erp)
        {
            _seed = seed;
            _erp = erp;
        }

        public int Calls { get; private set; }

        public CreditCheckResponse CheckCredit(CreditCheckRequest request)
        {
            lock (_gate)
            {
                Calls++;
            }

            var account = Account(request.SoldTo);
            if (account == null)
            {
                return new CreditCheckResponse { Approved = false, BlockReason = "Unknown sold-to " + request.SoldTo };
            }

            var exposure = account.OpenExposure + _erp.OpenOrderValue(account.ErpNumber);
            var approved = !account.Blocked && exposure + request.OrderValue <= account.CreditLimit;
            return new CreditCheckResponse
            {
                Approved = approved,
                CreditLimit = account.CreditLimit,
                OpenExposure = exposure,
                BlockReason = account.Blocked ? account.BlockReason : approved ? null : "Credit limit exceeded"
            };
        }

        public PriceOrderResponse PriceOrder(PriceOrderRequest request)
        {
            var lines = (request.Lines ?? Array.Empty<PriceLine>()).Select(l =>
            {
                var item = _seed.Item(l.Material);
                return new PriceLine
                {
                    LineNumber = l.LineNumber,
                    Material = l.Material,
                    Quantity = l.Quantity,
                    Unit = item?.Uom ?? l.Unit,
                    NetPrice = item?.Price ?? 0m,
                    PriceUnit = 1,
                    Blocked = item == null
                };
            }).ToArray();

            var account = Account(request.SoldTo);
            return new PriceOrderResponse
            {
                Lines = lines,
                NetValue = lines.Sum(l => Math.Round(l.Quantity * l.NetPrice, 2)),
                Currency = account?.Currency ?? "USD"
            };
        }

        public OrderStatusResponse GetOrderStatus(OrderStatusRequest request)
        {
            var order = _erp.FindOrder(request.SoldTo, request.CustomerReference, request.ErpOrderNumber);
            if (order == null)
            {
                return new OrderStatusResponse { Found = false };
            }

            return new OrderStatusResponse
            {
                Found = true,
                ErpOrderNumber = order.ErpOrderNumber,
                Status = order.Status,
                Created = order.Created,
                DeliveryNumber = order.DeliveryNumber,
                InvoiceNumber = order.InvoiceNumber
            };
        }

        private ErpAccount Account(string soldTo)
        {
            var key = (soldTo ?? string.Empty).TrimStart('0');
            return _seed.ErpAccounts.FirstOrDefault(a => string.Equals(a.ErpNumber.TrimStart('0'), key, StringComparison.Ordinal));
        }

        public static string Describe(CreditCheckResponse r)
        {
            return string.Format(CultureInfo.InvariantCulture, "approved={0} limit={1:0.00} exposure={2:0.00}{3}", r.Approved, r.CreditLimit, r.OpenExposure,
                string.IsNullOrEmpty(r.BlockReason) ? string.Empty : " block=" + r.BlockReason);
        }
    }
}
