using System.Collections.Generic;

namespace VanArsdel.Integration.Edi
{
    /// <summary>
    /// Outbound control numbers per partner and level. The BizTalk agreement keeps its own counters for documents
    /// the EDI assembler envelopes; this sequence is used for the documents enveloped by our own code (lab runner,
    /// resubmission of acknowledgements) and is backed by dbo.usp_NextControlNumber in production.
    /// </summary>
    public interface IControlNumberSequence
    {
        long Next(string partnerId, string level);
    }

    public class InMemoryControlNumberSequence : IControlNumberSequence
    {
        private readonly Dictionary<string, long> _values = new Dictionary<string, long>();
        private readonly long _start;
        private readonly object _gate = new object();

        public InMemoryControlNumberSequence(long start = 1)
        {
            _start = start;
        }

        public long Next(string partnerId, string level)
        {
            lock (_gate)
            {
                var key = partnerId + "|" + level;
                long value;
                value = _values.TryGetValue(key, out value) ? value + 1 : _start;
                if (value > 999999999)
                {
                    value = 1;
                }

                _values[key] = value;
                return value;
            }
        }
    }
}
