using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.Integration.Edi.Validation;

namespace VanArsdel.Integration.Edi.Envelopes
{
    /// <summary>ISA/UNB envelope values.</summary>
    public class InterchangeHeader
    {
        public EdiStandard Standard { get; set; }
        public string SenderQualifier { get; set; }
        public string SenderId { get; set; }
        public string ReceiverQualifier { get; set; }
        public string ReceiverId { get; set; }
        public string ControlNumber { get; set; }
        public DateTime Prepared { get; set; }

        /// <summary>ISA12 (00401) or UNB syntax level (UNOC:3).</summary>
        public string Version { get; set; }

        public bool AckRequested { get; set; }
        public bool Test { get; set; }

        public override string ToString()
        {
            return Standard + " " + SenderId + " -> " + ReceiverId + " #" + ControlNumber;
        }
    }

    public class FunctionalGroup
    {
        public string FunctionalId { get; set; }
        public string SenderCode { get; set; }
        public string ReceiverCode { get; set; }
        public string ControlNumber { get; set; }
        public string Version { get; set; }
        public DateTime Prepared { get; set; }

        public List<TransactionSet> Transactions { get; } = new List<TransactionSet>();
    }

    public class TransactionSet
    {
        public string Type { get; set; }
        public string ControlNumber { get; set; }

        /// <summary>Message version for EDIFACT (D96A); X12 sets take the group version.</summary>
        public string Version { get; set; }

        public List<Segment> Segments { get; } = new List<Segment>();
        public List<EdiError> Errors { get; } = new List<EdiError>();

        public bool HasErrors
        {
            get { return Errors.Any(e => e.Severity == EdiErrorSeverity.Error); }
        }
    }

    public class ParsedInterchange
    {
        public InterchangeHeader Header { get; set; }
        public Delimiters Delimiters { get; set; }
        public List<FunctionalGroup> Groups { get; } = new List<FunctionalGroup>();

        /// <summary>Envelope errors (count or control number mismatch in IEA/GE/UNZ, unknown segments outside sets).</summary>
        public List<EdiError> EnvelopeErrors { get; } = new List<EdiError>();

        public IEnumerable<TransactionSet> AllTransactions
        {
            get { return Groups.SelectMany(g => g.Transactions); }
        }
    }
}
