using System.Collections.Generic;

namespace Coho.Billing.Core.Reads
{
    public class ReadFile
    {
        public ReadFile()
        {
            Reads = new List<MeterRead>();
            RejectedLines = new List<string>();
        }

        public string FileName { get; set; }
        public string BatchId { get; set; }
        public ReadSource Source { get; set; }
        public List<MeterRead> Reads { get; private set; }

        /// <summary>Lines that could not be used; the rest of the file is still imported.</summary>
        public List<string> RejectedLines { get; private set; }
    }

    public interface IReadFileParser
    {
        bool CanParse(string fileName, string firstLine);
        ReadFile Parse(string fileName, IEnumerable<string> lines);
    }
}
