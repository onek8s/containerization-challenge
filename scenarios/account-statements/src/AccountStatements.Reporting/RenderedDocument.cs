using Woodgrove.Statements.Core.Reports;

namespace Woodgrove.Statements.Reporting
{
    public class RenderedDocument
    {
        public byte[] Content { get; set; }
        public OutputFormat Format { get; set; }
        public string FileName { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerNo { get; set; }
        public int PageCount { get; set; }

        public string MimeType
        {
            get { return OutputFormats.MimeType(Format); }
        }
    }
}
