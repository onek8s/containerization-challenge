using System;
using System.Collections.Generic;
using System.Linq;
using X = VanArsdel.Integration.Edi.Definitions.X12Segments;
using F = VanArsdel.Integration.Edi.Definitions.EdifactSegments;

namespace VanArsdel.Integration.Edi.Definitions
{
    /// <summary>
    /// The transaction sets the platform exchanges. Segment and loop names follow the BizTalk EDI schemas
    /// (X12_00401_850, EFACT_D96A_ORDERS, ...) so the XML the pipeline runner produces is what the maps expect.
    /// </summary>
    public static class TransactionCatalog
    {
        private static readonly List<TransactionDefinition> Definitions = new List<TransactionDefinition>();

        static TransactionCatalog()
        {
            Definitions.Add(new TransactionDefinition(EdiStandard.X12, "00401", "850", "PO",
                S(X.ST, 1, 1), S(X.BEG, 1, 1), S(X.CUR, 0, 1), S(X.REF, 0, 12), S(X.PER, 0, 3), S(X.FOB, 0, 1), S(X.ITD, 0, 1), S(X.DTM, 0, 10), S(X.TD5, 0, 12),
                L("N9Loop1", 0, 1000, S(X.N9, 1, 1), S(X.MSG, 0, 1000)),
                L("N1Loop1", 0, 200, S(X.N1, 1, 1), S(X.N2, 0, 2), S(X.N3, 0, 2), S(X.N4, 0, 1), S(X.REF, 0, 12), S(X.PER, 0, 3)),
                L("PO1Loop1", 1, 100000, S(X.PO1, 1, 1), S(X.CUR, 0, 1), L("PIDLoop1", 0, 1000, S(X.PID, 1, 1)), S(X.REF, 0, 12), S(X.DTM, 0, 10)),
                L("CTTLoop1", 0, 1, S(X.CTT, 1, 1), S(X.AMT, 0, 1)),
                S(X.SE, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.X12, "00401", "810", "IN",
                S(X.ST, 1, 1), S(X.BIG, 1, 1), S(X.CUR, 0, 1), S(X.REF, 0, 12),
                L("N1Loop1", 0, 200, S(X.N1, 1, 1), S(X.N2, 0, 2), S(X.N3, 0, 2), S(X.N4, 0, 1), S(X.REF, 0, 12)),
                S(X.ITD, 0, 1), S(X.DTM, 0, 10), S(X.FOB, 0, 1),
                L("IT1Loop1", 1, 200000, S(X.IT1, 1, 1), S(X.TXI, 0, 10), L("PIDLoop1", 0, 1000, S(X.PID, 1, 1)), S(X.REF, 0, 12)),
                S(X.TDS, 1, 1), S(X.TXI, 0, 10),
                L("SACLoop2", 0, 25, S(X.SAC, 1, 1)),
                L("ISSLoop1", 0, 100, S(X.ISS, 1, 1)),
                S(X.CTT, 0, 1),
                S(X.SE, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.X12, "00401", "856", "SH",
                S(X.ST, 1, 1), S(X.BSN, 1, 1), S(X.DTM, 0, 10),
                L("HLLoop1", 1, 200000, S(X.HL, 1, 1), S(X.LIN, 0, 1), S(X.SN1, 0, 1), S(X.PRF, 0, 1), S(X.TD1, 0, 20), S(X.TD5, 0, 12), S(X.REF, 0, 200),
                    S(X.MAN, 0, 10), S(X.DTM, 0, 10), L("N1Loop1", 0, 200, S(X.N1, 1, 1), S(X.N2, 0, 2), S(X.N3, 0, 2), S(X.N4, 0, 1))),
                S(X.CTT, 0, 1),
                S(X.SE, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.X12, "00401", "997", "FA",
                S(X.ST, 1, 1), S(X.AK1, 1, 1),
                L("AK2Loop1", 0, 999999, S(X.AK2, 1, 1), L("AK3Loop1", 0, 999999, S(X.AK3, 1, 1), S(X.AK4, 0, 99)), S(X.AK5, 1, 1)),
                S(X.AK9, 1, 1),
                S(X.SE, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.Edifact, "D96A", "ORDERS", null,
                S(F.UNH, 1, 1), S(F.BGM, 1, 1), S(F.DTM, 1, 35), S(F.FTX, 0, 99),
                L("RFFLoop1", 0, 10, S(F.RFF, 1, 1), S(F.DTM, 0, 5)),
                L("NADLoop1", 0, 99, S(F.NAD, 1, 1), L("RFFLoop2", 0, 10, S(F.RFF, 1, 1)), L("CTALoop1", 0, 5, S(F.CTA, 1, 1), S(F.COM, 0, 5))),
                L("CUXLoop1", 0, 5, S(F.CUX, 1, 1)),
                L("TDTLoop1", 0, 10, S(F.TDT, 1, 1)),
                L("LINLoop1", 0, 200000, S(F.LIN, 1, 1), S(F.PIA, 0, 25), S(F.IMD, 0, 99), S(F.QTY, 0, 99), S(F.DTM, 0, 35), S(F.FTX, 0, 99),
                    L("PRILoop1", 0, 25, S(F.PRI, 1, 1))),
                S(F.UNS, 1, 1), S(F.CNT, 0, 10),
                S(F.UNT, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.Edifact, "D96A", "INVOIC", null,
                S(F.UNH, 1, 1), S(F.BGM, 1, 1), S(F.DTM, 1, 35), S(F.FTX, 0, 99),
                L("RFFLoop1", 0, 99, S(F.RFF, 1, 1), S(F.DTM, 0, 5)),
                L("NADLoop1", 0, 99, S(F.NAD, 1, 1), L("RFFLoop2", 0, 9999, S(F.RFF, 1, 1))),
                L("TAXLoop1", 0, 5, S(F.TAX, 1, 1)),
                L("CUXLoop1", 0, 5, S(F.CUX, 1, 1)),
                L("PATLoop1", 0, 10, S(F.PAT, 1, 1), S(F.DTM, 0, 5)),
                L("LINLoop1", 0, 9999999, S(F.LIN, 1, 1), S(F.PIA, 0, 25), S(F.IMD, 0, 99), S(F.QTY, 1, 5),
                    L("MOALoop1", 0, 99, S(F.MOA, 1, 1)), L("PRILoop1", 0, 25, S(F.PRI, 1, 1)), L("RFFLoop3", 0, 10, S(F.RFF, 1, 1)),
                    L("TAXLoop3", 0, 99, S(F.TAX, 1, 1), S(F.MOA, 0, 1))),
                S(F.UNS, 1, 1), S(F.CNT, 0, 10),
                L("MOALoop4", 1, 100, S(F.MOA, 1, 1)),
                L("TAXLoop5", 0, 10, S(F.TAX, 1, 1), S(F.MOA, 0, 2)),
                S(F.UNT, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.Edifact, "D96A", "DESADV", null,
                S(F.UNH, 1, 1), S(F.BGM, 1, 1), S(F.DTM, 1, 10),
                L("RFFLoop1", 0, 10, S(F.RFF, 1, 1), S(F.DTM, 0, 1)),
                L("NADLoop1", 0, 99, S(F.NAD, 1, 1)),
                L("TDTLoop1", 0, 10, S(F.TDT, 1, 1)),
                L("CPSLoop1", 0, 9999, S(F.CPS, 1, 1),
                    L("PACLoop1", 0, 99, S(F.PAC, 1, 1), L("PCILoop1", 0, 10, S(F.PCI, 1, 1), S(F.GIN, 0, 10))),
                    L("LINLoop1", 0, 9999, S(F.LIN, 1, 1), S(F.PIA, 0, 10), S(F.IMD, 0, 25), S(F.QTY, 0, 10), L("RFFLoop2", 0, 10, S(F.RFF, 1, 1), S(F.DTM, 0, 1)))),
                S(F.CNT, 0, 5),
                S(F.UNT, 1, 1)));

            Definitions.Add(new TransactionDefinition(EdiStandard.Edifact, "D3", "CONTRL", null,
                S(F.UNH, 1, 1), S(F.UCI, 1, 1),
                L("UCMLoop1", 0, 999999, S(F.UCM, 1, 1)),
                S(F.UNT, 1, 1)));
        }

        public static IEnumerable<TransactionDefinition> All
        {
            get { return Definitions; }
        }

        public static TransactionDefinition Find(EdiStandard standard, string type)
        {
            return Definitions.FirstOrDefault(d => d.Standard == standard && string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase));
        }

        public static TransactionDefinition Get(EdiStandard standard, string type)
        {
            var d = Find(standard, type);
            if (d == null)
            {
                throw new EdiException("No definition for " + standard + " " + type + ".");
            }

            return d;
        }

        public static TransactionDefinition FindByRoot(string rootName)
        {
            return Definitions.FirstOrDefault(d => d.RootName == rootName);
        }

        private static SegmentUse S(SegmentDef def, int min, int max)
        {
            return new SegmentUse(def, min, max);
        }

        private static LoopDef L(string name, int min, int max, params Node[] children)
        {
            return new LoopDef(name, min, max, children);
        }
    }
}
