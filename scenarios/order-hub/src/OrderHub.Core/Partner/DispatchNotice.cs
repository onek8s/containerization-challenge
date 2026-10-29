using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Litware.OrderHub.Core.Partner
{
    /// <summary>Dispatch advice returned to Contoso Retail on the dispatch queue.</summary>
    public class DispatchNotice
    {
        [JsonProperty("poNumber")] public string PoNumber { get; set; }
        [JsonProperty("supplierOrderNo")] public string SupplierOrderNo { get; set; }
        [JsonProperty("invoiceNo")] public string InvoiceNo { get; set; }
        [JsonProperty("carrier")] public string Carrier { get; set; }
        [JsonProperty("trackingNo")] public string TrackingNo { get; set; }
        [JsonProperty("shippedAt")] public DateTime ShippedAtUtc { get; set; }
        [JsonProperty("lines")] public List<DispatchLine> Lines { get; set; } = new List<DispatchLine>();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                DateFormatString = "yyyy-MM-dd'T'HH:mm:ss'Z'",
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        /// <summary>The partner keys its POs as "PO/store"; only the PO part goes back.</summary>
        public static string PoFromCustomerReference(string customerReference)
        {
            if (string.IsNullOrEmpty(customerReference)) return customerReference;
            int slash = customerReference.IndexOf('/');
            return slash < 0 ? customerReference : customerReference.Substring(0, slash);
        }
    }

    public class DispatchLine
    {
        [JsonProperty("articleNo")] public string ArticleNo { get; set; }
        [JsonProperty("qty")] public int Qty { get; set; }
    }
}
