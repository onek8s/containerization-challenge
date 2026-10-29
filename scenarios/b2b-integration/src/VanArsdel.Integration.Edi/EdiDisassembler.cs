using System.Collections.Generic;
using System.Linq;
using System.Xml;
using VanArsdel.Integration.Edi.Definitions;
using VanArsdel.Integration.Edi.Envelopes;
using VanArsdel.Integration.Edi.Validation;

namespace VanArsdel.Integration.Edi
{
    public class DisassembledTransaction
    {
        public TransactionSet Set { get; set; }
        public FunctionalGroup Group { get; set; }
        public TransactionDefinition Definition { get; set; }
        public XmlDocument Document { get; set; }

        public bool Accepted
        {
            get { return Document != null && !Set.HasErrors; }
        }
    }

    public class DisassemblyResult
    {
        public ParsedInterchange Interchange { get; set; }
        public List<DisassembledTransaction> Transactions { get; } = new List<DisassembledTransaction>();

        public bool HasEnvelopeErrors
        {
            get { return Interchange.EnvelopeErrors.Count > 0; }
        }

        public IEnumerable<DisassembledTransaction> Accepted
        {
            get { return Transactions.Where(t => t.Accepted); }
        }

        public IEnumerable<DisassembledTransaction> Rejected
        {
            get { return Transactions.Where(t => !t.Accepted); }
        }
    }

    /// <summary>Parse, split and convert to XML in one step, as the EDI receive pipeline does.</summary>
    public static class EdiDisassembler
    {
        public static DisassemblyResult Disassemble(string text)
        {
            var interchange = InterchangeParser.Parse(text);
            var result = new DisassemblyResult { Interchange = interchange };
            foreach (var group in interchange.Groups)
            {
                foreach (var set in group.Transactions)
                {
                    var def = TransactionCatalog.Find(interchange.Header.Standard, set.Type);
                    var item = new DisassembledTransaction { Set = set, Group = group, Definition = def };
                    if (def == null)
                    {
                        set.Errors.Add(new EdiError(set.Segments[0].Tag, 1, 1, EdiErrorCodes.InvalidCode, "Transaction set type " + set.Type + " is not supported."));
                    }
                    else
                    {
                        item.Document = new EdiXmlWriter(def).Write(set);
                    }

                    result.Transactions.Add(item);
                }
            }

            return result;
        }
    }
}
