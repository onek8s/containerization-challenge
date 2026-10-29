using System;
using System.Collections.Generic;

namespace Coho.Billing.Core.Rating
{
    public class TariffBlock
    {
        /// <summary>Upper limit of the block in m3 per 30 days; null for the last block.</summary>
        public decimal? UpTo { get; set; }
        public decimal Price { get; set; }
    }

    /// <summary>One version of a tariff, valid from a date until the next version.</summary>
    public class Tariff
    {
        public Tariff()
        {
            WaterBlocks = new List<TariffBlock>();
        }

        public string Code { get; set; }
        public string Name { get; set; }
        public DateTime ValidFrom { get; set; }
        public decimal StandingChargePerDay { get; set; }
        public List<TariffBlock> WaterBlocks { get; private set; }
        public decimal WastewaterPrice { get; set; }

        /// <summary>Share of water consumption charged as wastewater (1.0 = all, 0 = no sewer connection).</summary>
        public decimal WastewaterShare { get; set; } = 1m;
        public decimal VatRate { get; set; }
    }
}
