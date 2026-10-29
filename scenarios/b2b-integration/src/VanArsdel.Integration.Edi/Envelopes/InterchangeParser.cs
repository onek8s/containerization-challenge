using System;
using System.Globalization;
using System.Linq;
using VanArsdel.Integration.Edi.Validation;

namespace VanArsdel.Integration.Edi.Envelopes
{
    /// <summary>
    /// Splits an interchange into functional groups and transaction sets and checks the envelope counts. This is
    /// the part of the BizTalk EDI disassembler the receive pipeline runner needs; the result feeds EdiXmlWriter.
    /// </summary>
    public static class InterchangeParser
    {
        public static ParsedInterchange Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new EdiException("The interchange is empty.");
            }

            var standard = EdiTokenizer.Detect(text);
            var delimiters = EdiTokenizer.ReadDelimiters(text, standard);
            var segments = EdiTokenizer.Tokenize(text, delimiters);
            return standard == EdiStandard.X12 ? ParseX12(segments, delimiters) : ParseEdifact(segments, delimiters);
        }

        private static ParsedInterchange ParseX12(System.Collections.Generic.IList<Segment> segments, Delimiters d)
        {
            var isa = segments[0];
            var result = new ParsedInterchange
            {
                Delimiters = d,
                Header = new InterchangeHeader
                {
                    Standard = EdiStandard.X12,
                    SenderQualifier = isa[5].Trim(),
                    SenderId = isa[6].Trim(),
                    ReceiverQualifier = isa[7].Trim(),
                    ReceiverId = isa[8].Trim(),
                    Prepared = ParseDate(isa[9], isa[10], "yyMMdd"),
                    Version = isa[12].Trim(),
                    ControlNumber = isa[13].Trim(),
                    AckRequested = isa[14].Trim() == "1",
                    Test = isa[15].Trim() == "T"
                }
            };

            FunctionalGroup group = null;
            TransactionSet set = null;
            var groupCount = 0;
            for (var i = 1; i < segments.Count; i++)
            {
                var s = segments[i];
                switch (s.Tag)
                {
                    case "GS":
                        group = new FunctionalGroup
                        {
                            FunctionalId = s[1],
                            SenderCode = s[2],
                            ReceiverCode = s[3],
                            Prepared = ParseDate(s[4], s[5], "yyyyMMdd"),
                            ControlNumber = s[6],
                            Version = s[8]
                        };
                        result.Groups.Add(group);
                        groupCount++;
                        break;
                    case "ST":
                        if (group == null)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("ST outside a functional group.", "024"));
                            group = new FunctionalGroup();
                            result.Groups.Add(group);
                        }

                        set = new TransactionSet { Type = s[1], ControlNumber = s[2], Version = group.Version };
                        s.Position = 1;
                        set.Segments.Add(s);
                        break;
                    case "SE":
                        if (set == null)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("SE without ST.", "024"));
                            break;
                        }

                        s.Position = set.Segments.Count + 1;
                        set.Segments.Add(s);
                        CheckTrailer(set, s[1], s[2], EdiStandard.X12);
                        group.Transactions.Add(set);
                        set = null;
                        break;
                    case "GE":
                        if (group != null)
                        {
                            if (ToInt(s[1]) != group.Transactions.Count)
                            {
                                result.EnvelopeErrors.Add(EdiError.Envelope("GE01 is " + s[1] + " but the group has " + group.Transactions.Count + " transaction sets.", "5"));
                            }

                            if (s[2] != group.ControlNumber)
                            {
                                result.EnvelopeErrors.Add(EdiError.Envelope("GE02 " + s[2] + " does not match GS06 " + group.ControlNumber + ".", "4"));
                            }
                        }

                        group = null;
                        break;
                    case "IEA":
                        if (ToInt(s[1]) != groupCount)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("IEA01 is " + s[1] + " but the interchange has " + groupCount + " groups.", "021"));
                        }

                        if (s[2].Trim() != result.Header.ControlNumber)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("IEA02 " + s[2] + " does not match ISA13 " + result.Header.ControlNumber + ".", "001"));
                        }

                        break;
                    default:
                        if (set == null)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("Segment " + s.Tag + " outside a transaction set.", "024"));
                            break;
                        }

                        s.Position = set.Segments.Count + 1;
                        set.Segments.Add(s);
                        break;
                }
            }

            if (set != null)
            {
                set.Errors.Add(new EdiError(set.Segments.Last().Tag, set.Segments.Count, 0, EdiErrorCodes.MissingSegment, "Transaction set " + set.ControlNumber + " has no SE trailer."));
                group.Transactions.Add(set);
            }

            return result;
        }

        private static ParsedInterchange ParseEdifact(System.Collections.Generic.IList<Segment> segments, Delimiters d)
        {
            var unb = segments[0];
            if (unb.Tag != "UNB")
            {
                throw new EdiException("Expected UNB, found " + unb.Tag + ".");
            }

            var result = new ParsedInterchange
            {
                Delimiters = d,
                Header = new InterchangeHeader
                {
                    Standard = EdiStandard.Edifact,
                    Version = unb.Component(1, 1) + ":" + unb.Component(1, 2),
                    SenderId = unb.Component(2, 1),
                    SenderQualifier = unb.Component(2, 2),
                    ReceiverId = unb.Component(3, 1),
                    ReceiverQualifier = unb.Component(3, 2),
                    Prepared = ParseDate(unb.Component(4, 1), unb.Component(4, 2), unb.Component(4, 1).Length == 8 ? "yyyyMMdd" : "yyMMdd"),
                    ControlNumber = unb[5],
                    AckRequested = unb[9] == "1",
                    Test = unb[11] == "1"
                }
            };

            // UNG groups are not used by our partners: one implicit group per interchange.
            var group = new FunctionalGroup { ControlNumber = result.Header.ControlNumber, Prepared = result.Header.Prepared };
            result.Groups.Add(group);
            TransactionSet set = null;
            for (var i = 1; i < segments.Count; i++)
            {
                var s = segments[i];
                switch (s.Tag)
                {
                    case "UNH":
                        set = new TransactionSet
                        {
                            Type = s.Component(2, 1),
                            ControlNumber = s[1],
                            Version = s.Component(2, 2) + s.Component(2, 3)
                        };
                        s.Position = 1;
                        set.Segments.Add(s);
                        break;
                    case "UNT":
                        if (set == null)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("UNT without UNH.", "13"));
                            break;
                        }

                        s.Position = set.Segments.Count + 1;
                        set.Segments.Add(s);
                        CheckTrailer(set, s[1], s[2], EdiStandard.Edifact);
                        group.Transactions.Add(set);
                        set = null;
                        break;
                    case "UNZ":
                        if (ToInt(s[1]) != group.Transactions.Count)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("UNZ count is " + s[1] + " but the interchange has " + group.Transactions.Count + " messages.", "13"));
                        }

                        if (s[2] != result.Header.ControlNumber)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("UNZ reference " + s[2] + " does not match UNB " + result.Header.ControlNumber + ".", "13"));
                        }

                        break;
                    default:
                        if (set == null)
                        {
                            result.EnvelopeErrors.Add(EdiError.Envelope("Segment " + s.Tag + " outside a message.", "13"));
                            break;
                        }

                        s.Position = set.Segments.Count + 1;
                        set.Segments.Add(s);
                        break;
                }
            }

            if (set != null)
            {
                set.Errors.Add(new EdiError("UNT", set.Segments.Count, 0, EdiErrorCodes.MissingSegment, "Message " + set.ControlNumber + " has no UNT trailer."));
                group.Transactions.Add(set);
            }

            return result;
        }

        private static void CheckTrailer(TransactionSet set, string count, string control, EdiStandard standard)
        {
            if (ToInt(count) != set.Segments.Count)
            {
                set.Errors.Add(new EdiError(standard == EdiStandard.X12 ? "SE" : "UNT", set.Segments.Count, 1, EdiErrorCodes.SegmentCount,
                    "Trailer segment count " + count + " but the set has " + set.Segments.Count + " segments."));
            }

            if (control != set.ControlNumber)
            {
                set.Errors.Add(new EdiError(standard == EdiStandard.X12 ? "SE" : "UNT", set.Segments.Count, 2, EdiErrorCodes.ControlNumberMismatch,
                    "Trailer control number " + control + " does not match " + set.ControlNumber + "."));
            }
        }

        private static DateTime ParseDate(string date, string time, string datePattern)
        {
            DateTime d;
            if (!DateTime.TryParseExact((date ?? string.Empty).Trim(), datePattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                return DateTime.MinValue;
            }

            var t = (time ?? string.Empty).Trim();
            int hh, mm;
            if (t.Length >= 4 && int.TryParse(t.Substring(0, 2), out hh) && int.TryParse(t.Substring(2, 2), out mm))
            {
                d = d.AddHours(hh).AddMinutes(mm);
            }

            return d;
        }

        private static int ToInt(string value)
        {
            int n;
            return int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : -1;
        }
    }
}
