namespace Wingtip.Invoices.Infrastructure.Data
{
    public class InboundChannel
    {
        public int ChannelId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; }

        /// <summary>FTP or SHARE.</summary>
        public string ChannelType { get; set; }

        /// <summary>FTP folder (relative to the FTP root) or UNC folder.</summary>
        public string Location { get; set; }

        public string FilePattern { get; set; }
        public int CodePage { get; set; }
    }

    public class SupplierInfo
    {
        public int SupplierId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string VatNumber { get; set; }
        public string VendorNumber { get; set; }
        public int CodePage { get; set; }
        public decimal? PriceTolerancePercent { get; set; }
        public bool TwoWayMatch { get; set; }
        public string CostCenter { get; set; }
    }
}
