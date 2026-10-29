using System;
using System.Collections.Generic;

namespace VanArsdel.Integration.Messaging
{
    public interface IDuplicateStore
    {
        /// <summary>Registers the key; returns false when it was already registered inside the window.</summary>
        bool TryRegister(string partnerId, string key, DateTime receivedUtc, TimeSpan window);
    }

    /// <summary>
    /// Duplicate check of the receive pipelines: partners resend whole interchanges after a VAN or SFTP problem.
    /// The key is the interchange control number when there is one, otherwise the body hash. Backed by
    /// dbo.ReceivedInterchange (usp_RegisterReceivedInterchange) in production.
    /// </summary>
    public class DuplicateDetector
    {
        private readonly IDuplicateStore _store;
        private readonly TimeSpan _window;

        public DuplicateDetector(IDuplicateStore store, TimeSpan window)
        {
            _store = store;
            _window = window;
        }

        public static string KeyFor(string interchangeControlNumber, string bodyHash)
        {
            return string.IsNullOrEmpty(interchangeControlNumber) ? "H:" + bodyHash : "C:" + interchangeControlNumber.TrimStart('0');
        }

        public bool IsDuplicate(string partnerId, string interchangeControlNumber, string bodyHash, DateTime receivedUtc)
        {
            return !_store.TryRegister(partnerId, KeyFor(interchangeControlNumber, bodyHash), receivedUtc, _window);
        }
    }

    public class InMemoryDuplicateStore : IDuplicateStore
    {
        private readonly Dictionary<string, DateTime> _seen = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly object _gate = new object();

        public bool TryRegister(string partnerId, string key, DateTime receivedUtc, TimeSpan window)
        {
            lock (_gate)
            {
                var k = partnerId + "|" + key;
                DateTime previous;
                if (_seen.TryGetValue(k, out previous) && receivedUtc - previous < window)
                {
                    return false;
                }

                _seen[k] = receivedUtc;
                return true;
            }
        }
    }
}
