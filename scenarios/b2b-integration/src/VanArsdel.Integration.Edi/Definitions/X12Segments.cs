using static VanArsdel.Integration.Edi.Definitions.ElementType;

namespace VanArsdel.Integration.Edi.Definitions
{
    /// <summary>X12 004010 segment dictionary (the subset our partners use).</summary>
    internal static class X12Segments
    {
        private static ElementDef E(string name, ElementType type, int min, int max, bool mandatory = false, string codes = null)
        {
            return new ElementDef(name, type, min, max, mandatory, codes == null ? null : codes.Split(','));
        }

        public static readonly SegmentDef ST = new SegmentDef("ST", "Transaction Set Header",
            E("ST01", ID, 3, 3, true, "810,850,855,856,997"), E("ST02", AN, 4, 9, true));

        public static readonly SegmentDef SE = new SegmentDef("SE", "Transaction Set Trailer",
            E("SE01", N0, 1, 10, true), E("SE02", AN, 4, 9, true));

        public static readonly SegmentDef BEG = new SegmentDef("BEG", "Beginning Segment for Purchase Order",
            E("BEG01", ID, 2, 2, true, "00,01,05,06"), E("BEG02", ID, 2, 2, true, "SA,DS,KN,RL,NE"), E("BEG03", AN, 1, 22, true),
            E("BEG04", AN, 1, 30), E("BEG05", DT, 8, 8, true));

        public static readonly SegmentDef CUR = new SegmentDef("CUR", "Currency",
            E("CUR01", ID, 2, 3, true, "BY,SE"), E("CUR02", ID, 3, 3, true));

        public static readonly SegmentDef REF = new SegmentDef("REF", "Reference Identification",
            E("REF01", ID, 2, 3, true, "DP,IA,PO,VN,CO,BM,CN,IV,ZZ"), E("REF02", AN, 1, 30), E("REF03", AN, 1, 80));

        public static readonly SegmentDef PER = new SegmentDef("PER", "Administrative Communications Contact",
            E("PER01", ID, 2, 2, true, "BD,IC,OC"), E("PER02", AN, 1, 60), E("PER03", ID, 2, 2, false, "TE,EM,FX"), E("PER04", AN, 1, 80));

        public static readonly SegmentDef FOB = new SegmentDef("FOB", "F.O.B. Related Instructions",
            E("FOB01", ID, 2, 2, true, "CC,PP,PC,DF"));

        public static readonly SegmentDef ITD = new SegmentDef("ITD", "Terms of Sale",
            E("ITD01", ID, 2, 2, false, "01,05,08,14"), E("ITD02", ID, 1, 2), E("ITD03", R, 1, 6), E("ITD04", DT, 8, 8), E("ITD05", N0, 1, 3),
            E("ITD06", DT, 8, 8), E("ITD07", N0, 1, 3));

        public static readonly SegmentDef DTM = new SegmentDef("DTM", "Date/Time Reference",
            E("DTM01", ID, 3, 3, true, "002,010,011,017,037,038,063,064,067,068"), E("DTM02", DT, 8, 8), E("DTM03", TM, 4, 8));

        public static readonly SegmentDef TD5 = new SegmentDef("TD5", "Carrier Details (Routing Sequence/Transit Time)",
            E("TD501", ID, 1, 2, false, "B,O"), E("TD502", ID, 1, 2, false, "2,92"), E("TD503", AN, 2, 80), E("TD504", ID, 1, 2, false, "M,U,LT,A,H"), E("TD505", AN, 1, 35));

        public static readonly SegmentDef N9 = new SegmentDef("N9", "Reference Identification",
            E("N901", ID, 2, 3, true), E("N902", AN, 1, 30));

        public static readonly SegmentDef MSG = new SegmentDef("MSG", "Message Text",
            E("MSG01", AN, 1, 264, true));

        public static readonly SegmentDef N1 = new SegmentDef("N1", "Name",
            E("N101", ID, 2, 3, true, "BT,BY,ST,SU,SF,VN,RI,RE,SE"), E("N102", AN, 1, 60), E("N103", ID, 1, 2, false, "1,9,91,92,UL"), E("N104", AN, 2, 80));

        public static readonly SegmentDef N2 = new SegmentDef("N2", "Additional Name Information",
            E("N201", AN, 1, 60, true), E("N202", AN, 1, 60));

        public static readonly SegmentDef N3 = new SegmentDef("N3", "Address Information",
            E("N301", AN, 1, 55, true), E("N302", AN, 1, 55));

        public static readonly SegmentDef N4 = new SegmentDef("N4", "Geographic Location",
            E("N401", AN, 2, 30), E("N402", ID, 2, 2), E("N403", ID, 3, 15), E("N404", ID, 2, 3));

        public static readonly SegmentDef PO1 = new SegmentDef("PO1", "Baseline Item Data",
            E("PO101", AN, 1, 20), E("PO102", R, 1, 15, true), E("PO103", ID, 2, 2, true), E("PO104", R, 1, 17), E("PO105", ID, 2, 2, false, "PE,CT,CA,PD"),
            E("PO106", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("PO107", AN, 1, 48), E("PO108", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("PO109", AN, 1, 48),
            E("PO110", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("PO111", AN, 1, 48));

        public static readonly SegmentDef PID = new SegmentDef("PID", "Product/Item Description",
            E("PID01", ID, 1, 1, true, "F,S,X"), E("PID02", ID, 2, 3), E("PID03", ID, 2, 2), E("PID04", AN, 1, 12), E("PID05", AN, 1, 80));

        public static readonly SegmentDef CTT = new SegmentDef("CTT", "Transaction Totals",
            E("CTT01", N0, 1, 6, true), E("CTT02", R, 1, 10));

        public static readonly SegmentDef AMT = new SegmentDef("AMT", "Monetary Amount",
            E("AMT01", ID, 1, 3, true, "TT,1,GV"), E("AMT02", R, 1, 18, true));

        // 810
        public static readonly SegmentDef BIG = new SegmentDef("BIG", "Beginning Segment for Invoice",
            E("BIG01", DT, 8, 8, true), E("BIG02", AN, 1, 22, true), E("BIG03", DT, 8, 8), E("BIG04", AN, 1, 22), E("BIG05", AN, 1, 30),
            E("BIG06", AN, 1, 8), E("BIG07", ID, 2, 2, false, "DI,CR,DR"));

        public static readonly SegmentDef IT1 = new SegmentDef("IT1", "Baseline Item Data (Invoice)",
            E("IT101", AN, 1, 20), E("IT102", R, 1, 10, true), E("IT103", ID, 2, 2, true), E("IT104", R, 1, 17, true), E("IT105", ID, 2, 2),
            E("IT106", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("IT107", AN, 1, 48), E("IT108", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("IT109", AN, 1, 48),
            E("IT110", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("IT111", AN, 1, 48));

        public static readonly SegmentDef TXI = new SegmentDef("TXI", "Tax Information",
            E("TXI01", ID, 2, 2, true, "ST,CT,LS,GS,VA"), E("TXI02", R, 1, 18), E("TXI03", R, 1, 10));

        public static readonly SegmentDef TDS = new SegmentDef("TDS", "Total Monetary Value Summary",
            E("TDS01", ElementType.N2, 1, 15, true), E("TDS02", ElementType.N2, 1, 15));

        public static readonly SegmentDef SAC = new SegmentDef("SAC", "Service, Promotion, Allowance, or Charge Information",
            E("SAC01", ID, 1, 1, true, "A,C,N"), E("SAC02", ID, 4, 4, false, "D240,C310,F050,H850"), E("SAC03", ID, 2, 2), E("SAC04", AN, 1, 10), E("SAC05", ElementType.N2, 1, 15));

        public static readonly SegmentDef ISS = new SegmentDef("ISS", "Invoice Shipment Summary",
            E("ISS01", R, 1, 15), E("ISS02", ID, 2, 2), E("ISS03", R, 1, 10), E("ISS04", ID, 2, 2));

        // 856
        public static readonly SegmentDef BSN = new SegmentDef("BSN", "Beginning Segment for Ship Notice",
            E("BSN01", ID, 2, 2, true, "00,05,06"), E("BSN02", AN, 2, 30, true), E("BSN03", DT, 8, 8, true), E("BSN04", TM, 4, 8, true), E("BSN05", ID, 4, 4, false, "0001,0002,0004"));

        public static readonly SegmentDef HL = new SegmentDef("HL", "Hierarchical Level",
            E("HL01", AN, 1, 12, true), E("HL02", AN, 1, 12), E("HL03", ID, 1, 2, true, "S,O,T,P,I"), E("HL04", ID, 1, 1, false, "0,1"));

        public static readonly SegmentDef LIN = new SegmentDef("LIN", "Item Identification",
            E("LIN01", AN, 1, 20), E("LIN02", ID, 2, 2, true, "BP,VP,UP,EN,UK,VN"), E("LIN03", AN, 1, 48, true), E("LIN04", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("LIN05", AN, 1, 48),
            E("LIN06", ID, 2, 2, false, "BP,VP,UP,EN,UK,VN"), E("LIN07", AN, 1, 48));

        public static readonly SegmentDef SN1 = new SegmentDef("SN1", "Item Detail (Shipment)",
            E("SN101", AN, 1, 20), E("SN102", R, 1, 10, true), E("SN103", ID, 2, 2, true), E("SN104", R, 1, 15), E("SN105", R, 1, 9), E("SN106", ID, 2, 2));

        public static readonly SegmentDef PRF = new SegmentDef("PRF", "Purchase Order Reference",
            E("PRF01", AN, 1, 22, true), E("PRF02", AN, 1, 30), E("PRF03", AN, 1, 30), E("PRF04", DT, 8, 8));

        public static readonly SegmentDef TD1 = new SegmentDef("TD1", "Carrier Details (Quantity and Weight)",
            E("TD101", AN, 3, 5, false), E("TD102", N0, 1, 7), E("TD103", ID, 1, 1), E("TD104", ID, 2, 2), E("TD105", AN, 1, 50), E("TD106", ID, 1, 2, false, "G,N"),
            E("TD107", R, 1, 10), E("TD108", ID, 2, 2, false, "KG,LB"));

        public static readonly SegmentDef MAN = new SegmentDef("MAN", "Marks and Numbers",
            E("MAN01", ID, 1, 2, true, "GM,CP,AA,W"), E("MAN02", AN, 1, 48, true));

        // 997
        public static readonly SegmentDef AK1 = new SegmentDef("AK1", "Functional Group Response Header",
            E("AK101", ID, 2, 2, true, "PO,IN,SH,PR,FA"), E("AK102", N0, 1, 9, true));

        public static readonly SegmentDef AK2 = new SegmentDef("AK2", "Transaction Set Response Header",
            E("AK201", ID, 3, 3, true), E("AK202", AN, 4, 9, true));

        public static readonly SegmentDef AK3 = new SegmentDef("AK3", "Data Segment Note",
            E("AK301", ID, 2, 3, true), E("AK302", N0, 1, 6, true), E("AK303", AN, 1, 6), E("AK304", ID, 1, 3, false, "1,2,3,4,5,6,7,8"));

        public static readonly SegmentDef AK4 = new SegmentDef("AK4", "Data Element Note",
            E("AK401", AN, 1, 2, true), E("AK402", N0, 1, 4), E("AK403", ID, 1, 3, true, "1,2,3,4,5,6,7,8,9,10,12,13"), E("AK404", AN, 1, 99));

        public static readonly SegmentDef AK5 = new SegmentDef("AK5", "Transaction Set Response Trailer",
            E("AK501", ID, 1, 1, true, "A,E,M,R,W,X"), E("AK502", ID, 1, 3), E("AK503", ID, 1, 3));

        public static readonly SegmentDef AK9 = new SegmentDef("AK9", "Functional Group Response Trailer",
            E("AK901", ID, 1, 1, true, "A,E,M,P,R,W,X"), E("AK902", N0, 1, 6, true), E("AK903", N0, 1, 6, true), E("AK904", N0, 1, 6, true), E("AK905", ID, 1, 3));
    }
}
