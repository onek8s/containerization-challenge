using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using VanArsdel.Integration.Edi.Definitions;
using VanArsdel.Integration.Edi.Envelopes;
using VanArsdel.Integration.Edi.Validation;

namespace VanArsdel.Integration.Edi.Acks
{
    /// <summary>
    /// Generates the technical acknowledgement for a received interchange: X12 997 (one per functional group)
    /// or EDIFACT CONTRL. The result is EDI XML, enveloped by InterchangeBuilder like any outbound document.
    /// </summary>
    public static class AckBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static IList<XmlDocument> Build(DisassemblyResult result)
        {
            return result.Interchange.Header.Standard == EdiStandard.X12
                ? result.Interchange.Groups.Select(g => Build997(result, g)).ToList()
                : new List<XmlDocument> { BuildContrl(result) };
        }

        public static XmlDocument Build997(DisassemblyResult result, FunctionalGroup group)
        {
            var def = TransactionCatalog.Get(EdiStandard.X12, "997");
            var set = new TransactionSet { Type = "997", ControlNumber = "0001" };
            set.Segments.Add(Segment.Of("ST", "997", "0001"));
            set.Segments.Add(Segment.Of("AK1", group.FunctionalId, group.ControlNumber));

            var accepted = 0;
            var items = result.Transactions.Where(t => t.Group == group).ToList();
            foreach (var t in items)
            {
                set.Segments.Add(Segment.Of("AK2", t.Set.Type, t.Set.ControlNumber));
                var errors = t.Set.Errors.Where(e => e.Severity == EdiErrorSeverity.Error).ToList();
                foreach (var bySegment in errors.Where(e => e.SegmentTag != null && e.Code != EdiErrorCodes.SegmentCount && e.Code != EdiErrorCodes.ControlNumberMismatch).GroupBy(e => new { e.SegmentTag, e.SegmentPosition }).Take(20))
                {
                    var segmentError = bySegment.FirstOrDefault(e => !EdiErrorCodes.IsElementCode(e.Code));
                    set.Segments.Add(Segment.Of("AK3", bySegment.Key.SegmentTag, bySegment.Key.SegmentPosition.ToString(Inv), null,
                        segmentError == null ? "8" : segmentError.Code));
                    foreach (var e in bySegment.Where(x => EdiErrorCodes.IsElementCode(x.Code)).Take(99))
                    {
                        set.Segments.Add(Segment.Of("AK4", e.ElementPosition.ToString(Inv), null, EdiErrorCodes.ToAk4(e.Code), Truncate(e.BadValue, 99)));
                    }
                }

                var trailerError = errors.FirstOrDefault(e => e.Code == EdiErrorCodes.SegmentCount || e.Code == EdiErrorCodes.ControlNumberMismatch);
                if (errors.Count == 0)
                {
                    accepted++;
                    set.Segments.Add(Segment.Of("AK5", "A"));
                }
                else
                {
                    set.Segments.Add(Segment.Of("AK5", "R", trailerError == null ? "5" : (trailerError.Code == EdiErrorCodes.SegmentCount ? "4" : "3")));
                }
            }

            string ak901;
            if (accepted == items.Count)
            {
                ak901 = "A";
            }
            else if (accepted == 0)
            {
                ak901 = "R";
            }
            else
            {
                ak901 = "P";
            }

            set.Segments.Add(Segment.Of("AK9", ak901, items.Count.ToString(Inv), items.Count.ToString(Inv), accepted.ToString(Inv)));
            set.Segments.Add(Segment.Of("SE", (set.Segments.Count + 1).ToString(Inv), "0001"));
            Renumber(set);
            return new EdiXmlWriter(def).Write(set);
        }

        public static XmlDocument BuildContrl(DisassemblyResult result)
        {
            var def = TransactionCatalog.Get(EdiStandard.Edifact, "CONTRL");
            var h = result.Interchange.Header;
            var set = new TransactionSet { Type = "CONTRL", ControlNumber = "1" };
            set.Segments.Add(new Segment("UNH", new[] { new[] { "1" }, new[] { "CONTRL", "D", "3", "UN" } }));

            var anyRejected = result.HasEnvelopeErrors || result.Transactions.Any(t => !t.Accepted);
            var interchangeLevel = result.HasEnvelopeErrors ? "4" : "7";
            set.Segments.Add(new Segment("UCI", new[]
            {
                new[] { h.ControlNumber },
                new[] { h.SenderId, h.SenderQualifier },
                new[] { h.ReceiverId, h.ReceiverQualifier },
                new[] { interchangeLevel },
                new[] { result.HasEnvelopeErrors ? EdiErrorCodes.ToEdifact(result.Interchange.EnvelopeErrors[0].Code) : null }
            }));

            // Only report messages individually when something was wrong (EANCOM CONTRL convention).
            if (anyRejected)
            {
                foreach (var t in result.Transactions)
                {
                    var first = t.Set.Errors.FirstOrDefault(e => e.Severity == EdiErrorSeverity.Error);
                    set.Segments.Add(new Segment("UCM", new[]
                    {
                        new[] { t.Set.ControlNumber },
                        new[] { t.Set.Type, "D", (t.Set.Version ?? "D96A").Length > 1 ? (t.Set.Version ?? "D96A").Substring(1) : "96A", "UN" },
                        new[] { t.Accepted ? "7" : "4" },
                        new[] { first == null ? null : EdiErrorCodes.ToEdifact(first.Code) }
                    }));
                }
            }

            set.Segments.Add(Segment.Of("UNT", (set.Segments.Count + 1).ToString(Inv), "1"));
            Renumber(set);
            return new EdiXmlWriter(def).Write(set);
        }

        private static void Renumber(TransactionSet set)
        {
            for (var i = 0; i < set.Segments.Count; i++)
            {
                set.Segments[i].Position = i + 1;
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}
