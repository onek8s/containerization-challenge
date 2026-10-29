using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Contracts.Gateway;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.Gateway.Forwarding
{
    public class BatchCheckResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// The checks a partner gets an immediate answer for. Business validation (items, prices, credit) happens later
    /// in BizTalk and comes back as an order rejection e-mail, exactly as for EDI partners.
    /// </summary>
    public class BatchChecker
    {
        public const int MaxOrdersPerBatch = 200;
        public const int MaxLinesPerOrder = 500;
        private readonly SchemaValidator _schemas;
        private readonly PartnerRepository _partners;

        public BatchChecker(SchemaValidator schemas, PartnerRepository partners)
        {
            _schemas = schemas;
            _partners = partners;
        }

        public BatchCheckResult Check(OrderBatch batch, string callerPartnerId)
        {
            var result = new BatchCheckResult();
            if (batch == null)
            {
                result.Errors.Add("The request has no OrderBatch.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(batch.BatchId))
            {
                result.Errors.Add("BatchId is required.");
            }

            if (!string.Equals(batch.PartnerId, callerPartnerId, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("PartnerId '" + batch.PartnerId + "' does not match the signed-in partner.");
            }

            var partner = _partners == null ? null : _partners.LoadPartners().FirstOrDefault(p => string.Equals(p.PartnerId, callerPartnerId, StringComparison.OrdinalIgnoreCase));
            if (_partners != null && (partner == null || !partner.Active))
            {
                result.Errors.Add("Partner " + callerPartnerId + " is not active for gateway orders.");
            }

            var orders = batch.Orders ?? new PartnerOrder[0];
            if (orders.Length == 0)
            {
                result.Errors.Add("The batch has no orders.");
            }
            else if (orders.Length > MaxOrdersPerBatch)
            {
                result.Errors.Add("A batch can hold at most " + MaxOrdersPerBatch + " orders.");
            }

            foreach (var dup in orders.Where(o => !string.IsNullOrEmpty(o.PoNumber)).GroupBy(o => o.PoNumber, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                result.Errors.Add("PO number " + dup.Key + " appears " + dup.Count() + " times.");
            }

            foreach (var order in orders)
            {
                CheckOrder(order, result);
            }

            if (result.Errors.Count == 0 && _schemas != null)
            {
                foreach (var issue in _schemas.Validate(Serialize(batch)))
                {
                    if (issue.Severity == XmlSeverityType.Error)
                    {
                        result.Errors.Add(issue.ToString());
                    }
                    else
                    {
                        result.Warnings.Add(issue.ToString());
                    }
                }
            }

            return result;
        }

        public static byte[] Serialize(OrderBatch batch)
        {
            var serializer = new XmlSerializer(typeof(OrderBatch));
            var ns = new XmlSerializerNamespaces();
            ns.Add("ns0", MessageTypes.PartnerOrdersNamespace);
            using (var buffer = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
                {
                    serializer.Serialize(writer, batch, ns);
                }

                return buffer.ToArray();
            }
        }

        private static void CheckOrder(PartnerOrder order, BatchCheckResult result)
        {
            var label = "Order " + (order.PoNumber ?? "(no PO number)");
            if (string.IsNullOrWhiteSpace(order.PoNumber))
            {
                result.Errors.Add(label + ": PoNumber is required.");
            }
            else if (order.PoNumber.Length > 22)
            {
                // the ERP keeps 22 characters of the customer reference (BSTKD)
                result.Errors.Add(label + ": PoNumber is longer than 22 characters.");
            }

            var items = order.Items ?? new PartnerOrderItem[0];
            if (items.Length == 0)
            {
                result.Errors.Add(label + ": no items.");
            }
            else if (items.Length > MaxLinesPerOrder)
            {
                result.Errors.Add(label + ": more than " + MaxLinesPerOrder + " items.");
            }

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (string.IsNullOrWhiteSpace(item.Sku))
                {
                    result.Errors.Add(label + " item " + (i + 1) + ": Sku is required.");
                }

                if (item.Qty <= 0)
                {
                    result.Errors.Add(label + " item " + (i + 1) + ": Qty must be positive.");
                }
                else if (decimal.Truncate(item.Qty) != item.Qty && string.Equals(item.Unit, "EA", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(label + " item " + (i + 1) + ": Qty must be whole for unit EA.");
                }
            }

            if (order.DeliverBySpecified && order.DeliverBy.Date < order.OrderDate.Date)
            {
                result.Warnings.Add(label + ": DeliverBy is before OrderDate and will be ignored.");
            }

            if (order.DeliveryAddress == null && string.IsNullOrWhiteSpace(order.AccountNumber))
            {
                result.Errors.Add(label + ": give either AccountNumber (a ship-to on file) or a DeliveryAddress.");
            }
        }
    }
}
