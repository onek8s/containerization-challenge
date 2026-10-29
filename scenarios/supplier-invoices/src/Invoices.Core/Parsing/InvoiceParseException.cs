using System;

namespace Wingtip.Invoices.Core.Parsing
{
    [Serializable]
    public class InvoiceParseException : Exception
    {
        public InvoiceParseException(string message)
            : base(message)
        {
        }

        public InvoiceParseException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected InvoiceParseException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
