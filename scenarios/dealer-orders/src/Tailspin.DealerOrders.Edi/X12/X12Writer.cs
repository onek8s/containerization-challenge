using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Tailspin.DealerOrders.Edi.X12
{
    public sealed class X12Party
    {
        public X12Party(string qualifier, string id, string applicationCode)
        {
            Qualifier = qualifier;
            Id = id;
            ApplicationCode = applicationCode;
        }

        public string Qualifier { get; }
        public string Id { get; }
        public string ApplicationCode { get; }
    }

    /// <summary>
    /// Writes one interchange with one functional group. Transaction segment lists are given without ST/SE; the
    /// writer adds the envelopes and counts.
    /// </summary>
    public sealed class X12Writer
    {
        private readonly X12Delimiters _delimiters;
        private readonly List<KeyValuePair<string, List<X12Segment>>> _transactions = new List<KeyValuePair<string, List<X12Segment>>>();

        public X12Writer(X12Delimiters delimiters = null)
        {
            _delimiters = delimiters ?? X12Delimiters.Default;
        }

        public void AddTransaction(string transactionSetId, List<X12Segment> body) =>
            _transactions.Add(new KeyValuePair<string, List<X12Segment>>(transactionSetId, body));

        public string Write(X12Party sender, X12Party receiver, int interchangeControl, int groupControl, string functionalId,
            DateTime timestamp, bool test, bool requestAck, string version = "004010")
        {
            if (_transactions.Count == 0) throw new InvalidOperationException("No transactions to write.");
            var sb = new StringBuilder();
            var isa = new X12Segment("ISA",
                "00", new string(' ', 10), "00", new string(' ', 10),
                sender.Qualifier.PadRight(2), sender.Id.PadRight(15).Substring(0, 15),
                receiver.Qualifier.PadRight(2), receiver.Id.PadRight(15).Substring(0, 15),
                timestamp.ToString("yyMMdd", CultureInfo.InvariantCulture), timestamp.ToString("HHmm", CultureInfo.InvariantCulture),
                _delimiters.Repetition.ToString(), "00401", interchangeControl.ToString("000000000", CultureInfo.InvariantCulture),
                requestAck ? "1" : "0", test ? "T" : "P", _delimiters.Component.ToString());
            sb.Append(isa.ToString(_delimiters)).Append('\n');

            sb.Append(new X12Segment("GS", functionalId, sender.ApplicationCode, receiver.ApplicationCode,
                timestamp.ToString("yyyyMMdd", CultureInfo.InvariantCulture), timestamp.ToString("HHmm", CultureInfo.InvariantCulture),
                groupControl.ToString(CultureInfo.InvariantCulture), "X", version).ToString(_delimiters)).Append('\n');

            var setNumber = 1;
            foreach (var t in _transactions)
            {
                var control = setNumber.ToString("0000", CultureInfo.InvariantCulture);
                sb.Append(new X12Segment("ST", t.Key, control).ToString(_delimiters)).Append('\n');
                foreach (var segment in t.Value) sb.Append(segment.ToString(_delimiters)).Append('\n');
                sb.Append(new X12Segment("SE", (t.Value.Count + 2).ToString(CultureInfo.InvariantCulture), control).ToString(_delimiters)).Append('\n');
                setNumber++;
            }

            sb.Append(new X12Segment("GE", _transactions.Count.ToString(CultureInfo.InvariantCulture), groupControl.ToString(CultureInfo.InvariantCulture)).ToString(_delimiters)).Append('\n');
            sb.Append(new X12Segment("IEA", "1", interchangeControl.ToString("000000000", CultureInfo.InvariantCulture)).ToString(_delimiters)).Append('\n');
            return sb.ToString();
        }
    }
}
