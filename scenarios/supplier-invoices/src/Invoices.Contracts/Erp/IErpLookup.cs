using Wingtip.Invoices.Core.Matching;

namespace Wingtip.Invoices.Contracts.Erp
{
    /// <summary>
    /// Read access to vendor and purchase order data in the ERP, published by the InvoiceErpLink
    /// service at tcp://&lt;host&gt;:8095/ErpLookup.rem.
    /// </summary>
    public interface IErpLookup
    {
        Vendor FindVendorByVatNumber(string vatNumber);

        Vendor GetVendor(string vendorNumber);

        /// <summary>Order with ordered, received and already invoiced quantities; null when not found.</summary>
        PurchaseOrder GetPurchaseOrder(string orderNumber);

        /// <summary>First day of the open accounts payable period.</summary>
        System.DateTime GetOpenPeriodStart();

        string Ping();
    }
}
