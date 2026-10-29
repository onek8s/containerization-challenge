using System.Collections.Generic;

namespace Wingtip.Invoices.Core.Parsing
{
    public interface IInvoiceParser
    {
        SourceFormat Format { get; }

        /// <summary>Parses all invoices contained in one supplier file.</summary>
        IList<InvoiceDocument> Parse(string content);
    }
}
