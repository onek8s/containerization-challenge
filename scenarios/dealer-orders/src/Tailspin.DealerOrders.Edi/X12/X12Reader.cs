using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.DealerOrders.Edi.X12
{
    /// <summary>
    /// Parses an X12 interchange. Delimiters come from the fixed-width ISA segment (element separator at position 3,
    /// component separator at 104, segment terminator at 105). Envelope counts and control numbers are checked and
    /// reported as errors rather than exceptions so that a 997 can be returned for the bad parts.
    /// </summary>
    public static class X12Reader
    {
        public static X12Interchange Read(string text, out List<X12Error> errors)
        {
            errors = new List<X12Error>();
            if (text == null) throw new ArgumentNullException(nameof(text));
            var start = text.IndexOf("ISA", StringComparison.Ordinal);
            if (start < 0) throw new X12FormatException("No ISA segment found.");
            text = text.Substring(start);
            if (text.Length < 106) throw new X12FormatException("ISA segment is truncated.");

            var element = text[3];
            var component = text[104];
            var segment = text[105];
            var isaFields = text.Substring(0, 105).Split(element);
            if (isaFields.Length != 17) throw new X12FormatException($"ISA must have 16 elements, found {isaFields.Length - 1}.");
            var delimiters = new X12Delimiters(element, segment, component, isaFields[11].Length == 1 ? isaFields[11][0] : 'U');

            var segments = text.Split(segment)
                .Select(s => s.Trim('\r', '\n', ' '))
                .Where(s => s.Length > 0)
                .Select(s =>
                {
                    var parts = s.Split(element);
                    return new X12Segment(parts[0], parts.Skip(1));
                })
                .ToList();

            var interchange = new X12Interchange { Isa = segments[0], Delimiters = delimiters };
            X12FunctionalGroup group = null;
            X12Transaction transaction = null;

            for (var i = 1; i < segments.Count; i++)
            {
                var s = segments[i];
                switch (s.Id)
                {
                    case "GS":
                        if (group != null) errors.Add(new X12Error(X12ErrorLevel.Group, group.ControlNumber, "2", "GE missing before next GS."));
                        group = new X12FunctionalGroup { Gs = s };
                        interchange.Groups.Add(group);
                        break;
                    case "ST":
                        if (group == null) throw new X12FormatException("ST outside of a functional group.");
                        if (transaction != null) errors.Add(new X12Error(X12ErrorLevel.Transaction, transaction.ControlNumber, "2", "SE missing before next ST."));
                        transaction = new X12Transaction { St = s };
                        group.Transactions.Add(transaction);
                        break;
                    case "SE":
                        if (transaction == null) throw new X12FormatException("SE without ST.");
                        transaction.Se = s;
                        var count = transaction.Segments.Count + 2;
                        if (s[1] != count.ToString())
                            errors.Add(new X12Error(X12ErrorLevel.Transaction, transaction.ControlNumber, "4", $"SE01 says {s[1]} segments, counted {count}."));
                        if (s[2] != transaction.ControlNumber)
                            errors.Add(new X12Error(X12ErrorLevel.Transaction, transaction.ControlNumber, "3", $"SE02 {s[2]} does not match ST02 {transaction.ControlNumber}."));
                        transaction = null;
                        break;
                    case "GE":
                        if (group == null) throw new X12FormatException("GE without GS.");
                        group.Ge = s;
                        if (s[1] != group.Transactions.Count.ToString())
                            errors.Add(new X12Error(X12ErrorLevel.Group, group.ControlNumber, "5", $"GE01 says {s[1]} transactions, found {group.Transactions.Count}."));
                        if (s[2] != group.ControlNumber)
                            errors.Add(new X12Error(X12ErrorLevel.Group, group.ControlNumber, "4", $"GE02 {s[2]} does not match GS06 {group.ControlNumber}."));
                        group = null;
                        break;
                    case "IEA":
                        interchange.Iea = s;
                        if (s[1] != interchange.Groups.Count.ToString())
                            errors.Add(new X12Error(X12ErrorLevel.Interchange, interchange.ControlNumber, "021", $"IEA01 says {s[1]} groups, found {interchange.Groups.Count}."));
                        if (s[2] != interchange.ControlNumber)
                            errors.Add(new X12Error(X12ErrorLevel.Interchange, interchange.ControlNumber, "001", $"IEA02 {s[2]} does not match ISA13."));
                        break;
                    default:
                        if (transaction == null) throw new X12FormatException($"Segment {s.Id} outside of a transaction set.");
                        transaction.Segments.Add(s);
                        break;
                }
            }

            if (interchange.Iea == null) errors.Add(new X12Error(X12ErrorLevel.Interchange, interchange.ControlNumber, "023", "IEA segment missing."));
            if (transaction != null) errors.Add(new X12Error(X12ErrorLevel.Transaction, transaction.ControlNumber, "2", "SE segment missing."));
            return interchange;
        }
    }
}
