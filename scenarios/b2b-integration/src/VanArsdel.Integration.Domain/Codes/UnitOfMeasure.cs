using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Domain.Codes
{
    /// <summary>
    /// Unit of measure codes in the three code lists we deal with: ERP (SAP-style ISO-like internal units), X12 355
    /// and UN/ECE Recommendation 20 used by EDIFACT. Partner specific units are in the cross-reference table first.
    /// </summary>
    public sealed class UnitOfMeasure
    {
        private static readonly List<UnitOfMeasure> Units = new List<UnitOfMeasure>
        {
            new UnitOfMeasure("ST", "EA", "PCE", "Piece", 1m),
            new UnitOfMeasure("ST", "PC", "C62", "Piece", 1m, primary: false),
            new UnitOfMeasure("KAR", "CA", "CT", "Carton"),
            new UnitOfMeasure("BOX", "BX", "BX", "Box"),
            new UnitOfMeasure("PAK", "PK", "PK", "Pack"),
            new UnitOfMeasure("ROL", "RL", "RO", "Roll"),
            new UnitOfMeasure("M", "LM", "MTR", "Metre"),
            new UnitOfMeasure("FT", "FT", "FOT", "Foot"),
            new UnitOfMeasure("PAL", "PL", "PF", "Pallet"),
            new UnitOfMeasure("KG", "KG", "KGM", "Kilogram"),
            new UnitOfMeasure("L", "LT", "LTR", "Litre"),
            new UnitOfMeasure("SET", "SET", "SET", "Set")
        };

        private UnitOfMeasure(string erp, string x12, string unece, string name, decimal? fixedFactor = null, bool primary = true)
        {
            Erp = erp;
            X12 = x12;
            UnEce = unece;
            Name = name;
            FixedFactorToBase = fixedFactor;
            Primary = primary;
        }

        public string Erp { get; private set; }
        public string X12 { get; private set; }
        public string UnEce { get; private set; }
        public string Name { get; private set; }
        public decimal? FixedFactorToBase { get; private set; }
        public bool Primary { get; private set; }

        public static IEnumerable<UnitOfMeasure> All
        {
            get { return Units; }
        }

        public static UnitOfMeasure FromX12(string code)
        {
            return Units.FirstOrDefault(u => string.Equals(u.X12, Clean(code), StringComparison.OrdinalIgnoreCase));
        }

        public static UnitOfMeasure FromUnEce(string code)
        {
            return Units.FirstOrDefault(u => string.Equals(u.UnEce, Clean(code), StringComparison.OrdinalIgnoreCase));
        }

        public static UnitOfMeasure FromErp(string code)
        {
            return Units.FirstOrDefault(u => u.Primary && string.Equals(u.Erp, Clean(code), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Metres to feet for US customers that order cable by the foot.</summary>
        public static decimal Convert(decimal quantity, string fromErp, string toErp)
        {
            if (string.Equals(fromErp, toErp, StringComparison.OrdinalIgnoreCase))
            {
                return quantity;
            }

            if (fromErp == "FT" && toErp == "M")
            {
                return Math.Round(quantity * 0.3048m, 3, MidpointRounding.AwayFromZero);
            }

            if (fromErp == "M" && toErp == "FT")
            {
                return Math.Round(quantity / 0.3048m, 3, MidpointRounding.AwayFromZero);
            }

            throw new InvalidOperationException("No fixed conversion from " + fromErp + " to " + toErp + "; use the material's alternative units.");
        }

        private static string Clean(string code)
        {
            return code == null ? null : code.Trim();
        }

        public override string ToString()
        {
            return Erp + " (" + Name + ")";
        }
    }
}
