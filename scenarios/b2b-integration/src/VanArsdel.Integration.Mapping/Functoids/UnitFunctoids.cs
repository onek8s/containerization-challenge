using VanArsdel.Integration.Domain.Codes;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Mapping.Functoids
{
    /// <summary>Scripting functoid class: unit of measure codes. Partner specific units come from the cross-reference first.</summary>
    public class UnitFunctoids
    {
        public string X12ToErp(string partnerId, string code)
        {
            var x = Xref(partnerId, code);
            if (x != null)
            {
                return x;
            }

            var u = UnitOfMeasure.FromX12(code);
            return u == null ? code : u.Erp;
        }

        public string UnEceToErp(string partnerId, string code)
        {
            var x = Xref(partnerId, code);
            if (x != null)
            {
                return x;
            }

            var u = UnitOfMeasure.FromUnEce(code);
            return u == null ? code : u.Erp;
        }

        /// <summary>Gateway orders use free text units ("each", "box", "m").</summary>
        public string TextToErp(string partnerId, string unit)
        {
            var x = Xref(partnerId, unit);
            if (x != null)
            {
                return x;
            }

            switch ((unit ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                case "ea":
                case "each":
                case "pc":
                case "pcs":
                case "piece":
                    return "ST";
                case "box":
                case "bx":
                    return "BOX";
                case "m":
                case "metre":
                case "meter":
                    return "M";
                case "roll":
                    return "ROL";
                case "pack":
                    return "PAK";
                default:
                    return unit.Trim().ToUpperInvariant();
            }
        }

        public string ErpToX12(string partnerId, string erp)
        {
            var back = XrefProviderLocator.Current.ToPartner(partnerId, CrossReferenceKind.UnitOfMeasure, erp);
            if (back != null)
            {
                return back;
            }

            var u = UnitOfMeasure.FromErp(erp);
            return u == null ? erp : u.X12;
        }

        public string ErpToUnEce(string partnerId, string erp)
        {
            var back = XrefProviderLocator.Current.ToPartner(partnerId, CrossReferenceKind.UnitOfMeasure, erp);
            if (back != null)
            {
                return back;
            }

            var u = UnitOfMeasure.FromErp(erp);
            return u == null ? erp : u.UnEce;
        }

        private static string Xref(string partnerId, string code)
        {
            return string.IsNullOrEmpty(code) ? null : XrefProviderLocator.Current.ToInternal(partnerId, CrossReferenceKind.UnitOfMeasure, code);
        }
    }
}
