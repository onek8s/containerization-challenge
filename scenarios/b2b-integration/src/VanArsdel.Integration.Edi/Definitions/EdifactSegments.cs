using static VanArsdel.Integration.Edi.Definitions.ElementType;

namespace VanArsdel.Integration.Edi.Definitions
{
    /// <summary>UN/EDIFACT D.96A segment dictionary (the subset of the EANCOM profiles our partners use) and CONTRL.</summary>
    internal static class EdifactSegments
    {
        private static ElementDef E(string name, ElementType type, int min, int max, bool mandatory = false, string codes = null)
        {
            return new ElementDef(name, type, min, max, mandatory, codes == null ? null : codes.Split(','));
        }

        private static ElementDef C(string name, bool mandatory, params ElementDef[] components)
        {
            return ElementDef.Composite(name, mandatory, components);
        }

        public static readonly SegmentDef UNH = new SegmentDef("UNH", "Message header",
            E("UNH1", AN, 1, 14, true),
            C("UNH2", true, E("UNH2.1", ID, 1, 6, true, "ORDERS,INVOIC,DESADV,ORDRSP,CONTRL"), E("UNH2.2", ID, 1, 3, true), E("UNH2.3", ID, 1, 3, true),
                E("UNH2.4", ID, 1, 2, true), E("UNH2.5", AN, 1, 6)));

        public static readonly SegmentDef UNT = new SegmentDef("UNT", "Message trailer",
            E("UNT1", N0, 1, 6, true), E("UNT2", AN, 1, 14, true));

        public static readonly SegmentDef BGM = new SegmentDef("BGM", "Beginning of message",
            C("C002", false, E("C00201", AN, 1, 3, false, "220,221,224,226,351,380,381,383"), E("C00202", AN, 1, 3), E("C00203", AN, 1, 3), E("C00204", AN, 1, 35)),
            E("BGM02", AN, 1, 35),
            E("BGM03", ID, 1, 3, false, "1,5,9,27,31"),
            E("BGM04", ID, 1, 3, false, "AB,NA"));

        public static readonly SegmentDef DTM = new SegmentDef("DTM", "Date/time/period",
            C("C507", true, E("C50701", ID, 1, 3, true, "2,3,4,35,10,11,13,17,63,64,131,132,137,171"), E("C50702", AN, 1, 35), E("C50703", ID, 1, 3, false, "101,102,203,204,718")));

        public static readonly SegmentDef FTX = new SegmentDef("FTX", "Free text",
            E("FTX01", ID, 1, 3, true, "AAI,DEL,PUR,ZZZ"), E("FTX02", ID, 1, 3), C("C107", false, E("C10701", AN, 1, 17)),
            C("C108", false, E("C10801", AN, 1, 512, true), E("C10802", AN, 1, 512), E("C10803", AN, 1, 512)));

        public static readonly SegmentDef RFF = new SegmentDef("RFF", "Reference",
            C("C506", true, E("C50601", ID, 1, 3, true, "ON,VN,DQ,CR,IV,AAK,CT,PL,AAU,API,VA"), E("C50602", AN, 1, 35), E("C50603", AN, 1, 6)));

        public static readonly SegmentDef NAD = new SegmentDef("NAD", "Name and address",
            E("NAD01", ID, 1, 3, true, "BY,SU,DP,IV,ST,SE,CA,UC,BT"),
            C("C082", false, E("C08201", AN, 1, 35, true), E("C08202", AN, 1, 3), E("C08203", ID, 1, 3, false, "9,91,92")),
            C("C058", false, E("C05801", AN, 1, 35)),
            C("C080", false, E("C08001", AN, 1, 35, true), E("C08002", AN, 1, 35)),
            C("C059", false, E("C05901", AN, 1, 35, true), E("C05902", AN, 1, 35)),
            E("NAD06", AN, 1, 35),
            E("NAD07", AN, 1, 9),
            E("NAD08", AN, 1, 9),
            E("NAD09", ID, 1, 3));

        public static readonly SegmentDef CTA = new SegmentDef("CTA", "Contact information",
            E("CTA01", ID, 1, 3, false, "OC,PD"), C("C056", false, E("C05601", AN, 1, 17), E("C05602", AN, 1, 35)));

        public static readonly SegmentDef COM = new SegmentDef("COM", "Communication contact",
            C("C076", true, E("C07601", AN, 1, 512, true), E("C07602", ID, 1, 3, true, "TE,EM,FX")));

        public static readonly SegmentDef CUX = new SegmentDef("CUX", "Currencies",
            C("C504", false, E("C50401", ID, 1, 3, true, "2"), E("C50402", ID, 1, 3), E("C50403", ID, 1, 3, false, "4,9")));

        public static readonly SegmentDef PAT = new SegmentDef("PAT", "Payment terms basis",
            E("PAT01", ID, 1, 3, true, "1,3,22"),
            C("C110", false, E("C11001", AN, 1, 17)),
            C("C112", false, E("C11201", ID, 1, 3, true, "5,66"), E("C11202", ID, 1, 3), E("C11203", ID, 1, 3, false, "D,M"), E("C11204", N0, 1, 3)));

        public static readonly SegmentDef TDT = new SegmentDef("TDT", "Details of transport",
            E("TDT01", ID, 1, 3, true, "20,12"), E("TDT02", AN, 1, 17), C("C220", false, E("C22001", ID, 1, 3, false, "30,10,40")), C("C228", false, E("C22801", AN, 1, 8)),
            C("C040", false, E("C04001", AN, 1, 17), E("C04002", AN, 1, 3), E("C04003", AN, 1, 3), E("C04004", AN, 1, 35)));

        public static readonly SegmentDef LIN = new SegmentDef("LIN", "Line item",
            E("LIN01", AN, 1, 6),
            E("LIN02", ID, 1, 3),
            C("C212", false, E("C21201", AN, 1, 35), E("C21202", ID, 1, 3, false, "EN,SRV,IN,SA")));

        public static readonly SegmentDef PIA = new SegmentDef("PIA", "Additional product id",
            E("PIA01", ID, 1, 3, true, "1,5"),
            C("C212", true, E("C21201", AN, 1, 35, true), E("C21202", ID, 1, 3, false, "IN,SA,BP,MF,SRV")),
            C("C212_2", false, E("C212_201", AN, 1, 35, true), E("C212_202", ID, 1, 3)));

        public static readonly SegmentDef IMD = new SegmentDef("IMD", "Item description",
            E("IMD01", ID, 1, 3, false, "F,C"), C("C272", false, E("C27201", ID, 1, 3)), C("C273", false, E("C27301", AN, 1, 17), E("C27302", AN, 1, 3), E("C27303", AN, 1, 3), E("C27304", AN, 1, 256)));

        public static readonly SegmentDef QTY = new SegmentDef("QTY", "Quantity",
            C("C186", true, E("C18601", ID, 1, 3, true, "12,21,46,47,59,192"), E("C18602", R, 1, 15, true), E("C18603", ID, 1, 3)));

        public static readonly SegmentDef PRI = new SegmentDef("PRI", "Price details",
            C("C509", false, E("C50901", ID, 1, 3, true, "AAA,AAB,AAE"), E("C50902", R, 1, 15), E("C50903", ID, 1, 3), E("C50904", ID, 1, 3), E("C50905", R, 1, 9), E("C50906", ID, 1, 3)));

        public static readonly SegmentDef MOA = new SegmentDef("MOA", "Monetary amount",
            C("C516", true, E("C51601", ID, 1, 3, true, "9,77,79,86,124,125,128,176,203"), E("C51602", R, 1, 35), E("C51603", ID, 1, 3)));

        public static readonly SegmentDef TAX = new SegmentDef("TAX", "Duty/tax/fee details",
            E("TAX01", ID, 1, 3, true, "7,5"), C("C241", false, E("C24101", ID, 1, 3, false, "VAT,GST")), C("C533", false, E("C53301", AN, 1, 6)),
            C("C243", false, E("C24301", AN, 1, 7), E("C24302", AN, 1, 3), E("C24303", AN, 1, 35), E("C24304", R, 1, 17)), E("TAX06", ID, 1, 3, false, "S,Z,E,AE"));

        public static readonly SegmentDef UNS = new SegmentDef("UNS", "Section control",
            E("UNS01", ID, 1, 1, true, "D,S"));

        public static readonly SegmentDef CNT = new SegmentDef("CNT", "Control total",
            C("C270", true, E("C27001", ID, 1, 3, true, "1,2,7,11"), E("C27002", R, 1, 18, true)));

        public static readonly SegmentDef CPS = new SegmentDef("CPS", "Consignment packing sequence",
            E("CPS01", AN, 1, 12, true), E("CPS02", AN, 1, 12), E("CPS03", ID, 1, 3, false, "1,3,4"));

        public static readonly SegmentDef PAC = new SegmentDef("PAC", "Package",
            E("PAC01", N0, 1, 8), C("C531", false, E("C53101", ID, 1, 3)), C("C202", false, E("C20201", AN, 1, 17, false, "201,PK,CT,BX,PX")));

        public static readonly SegmentDef PCI = new SegmentDef("PCI", "Package identification",
            E("PCI01", ID, 1, 3, false, "33E,30"));

        public static readonly SegmentDef GIN = new SegmentDef("GIN", "Goods identity number",
            E("GIN01", ID, 1, 3, true, "BJ,ML"), C("C208", true, E("C20801", AN, 1, 35, true), E("C20802", AN, 1, 35)));

        // CONTRL (syntax version 3)
        public static readonly SegmentDef UCI = new SegmentDef("UCI", "Interchange response",
            E("UCI01", AN, 1, 14, true),
            C("S002", true, E("S00201", AN, 1, 35, true), E("S00202", AN, 1, 4)),
            C("S003", true, E("S00301", AN, 1, 35, true), E("S00302", AN, 1, 4)),
            E("UCI04", ID, 1, 3, true, "4,7,8"),
            E("UCI05", ID, 1, 3));

        public static readonly SegmentDef UCM = new SegmentDef("UCM", "Message response",
            E("UCM01", AN, 1, 14, true),
            C("S009", true, E("S00901", AN, 1, 6, true), E("S00902", AN, 1, 3, true), E("S00903", AN, 1, 3, true), E("S00904", AN, 1, 2, true)),
            E("UCM03", ID, 1, 3, true, "4,7"),
            E("UCM04", ID, 1, 3));
    }
}
