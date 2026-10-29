using System;

namespace Coho.Billing.Core.Reads
{
    public class ReadFileException : Exception
    {
        public ReadFileException(string message, int lineNumber)
            : base(lineNumber > 0 ? string.Format("Line {0}: {1}", lineNumber, message) : message)
        {
            LineNumber = lineNumber;
        }

        public int LineNumber { get; private set; }
    }
}
