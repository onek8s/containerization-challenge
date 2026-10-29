using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.DealerOrders.Edi.X12
{
    public sealed class X12Interchange
    {
        public X12Segment Isa { get; set; }
        public X12Segment Iea { get; set; }
        public X12Delimiters Delimiters { get; set; }
        public List<X12FunctionalGroup> Groups { get; } = new List<X12FunctionalGroup>();

        public string SenderQualifier => Isa[5].Trim();
        public string SenderId => Isa[6].Trim();
        public string ReceiverQualifier => Isa[7].Trim();
        public string ReceiverId => Isa[8].Trim();
        public string ControlNumber => Isa[13];
        public bool AcknowledgmentRequested => Isa[14] == "1";
        public bool IsTest => Isa[15] == "T";
    }

    public sealed class X12FunctionalGroup
    {
        public X12Segment Gs { get; set; }
        public X12Segment Ge { get; set; }
        public List<X12Transaction> Transactions { get; } = new List<X12Transaction>();

        /// <summary>GS01 functional identifier code: PO (850), PR (855), SH (856), FA (997).</summary>
        public string FunctionalId => Gs[1];

        public string ApplicationSender => Gs[2];
        public string ApplicationReceiver => Gs[3];
        public string ControlNumber => Gs[6];
        public string Version => Gs[8];
    }

    public sealed class X12Transaction
    {
        public X12Segment St { get; set; }
        public X12Segment Se { get; set; }

        /// <summary>Segments between ST and SE.</summary>
        public List<X12Segment> Segments { get; } = new List<X12Segment>();

        public string TransactionSetId => St[1];
        public string ControlNumber => St[2];

        public X12Segment First(string id) => Segments.FirstOrDefault(s => s.Id == id);
        public IEnumerable<X12Segment> All(string id) => Segments.Where(s => s.Id == id);
    }

    public enum X12ErrorLevel
    {
        Interchange,
        Group,
        Transaction
    }

    public sealed class X12Error
    {
        public X12Error(X12ErrorLevel level, string controlNumber, string code, string message)
        {
            Level = level;
            ControlNumber = controlNumber;
            Code = code;
            Message = message;
        }

        public X12ErrorLevel Level { get; }
        public string ControlNumber { get; }

        /// <summary>AK5/AK9 syntax error code (e.g. 4 = number of included segments does not match actual count).</summary>
        public string Code { get; }

        public string Message { get; }

        public override string ToString() => $"{Level} {ControlNumber}: {Message} (code {Code})";
    }

    [Serializable]
    public class X12FormatException : Exception
    {
        public X12FormatException(string message) : base(message) { }
        protected X12FormatException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
