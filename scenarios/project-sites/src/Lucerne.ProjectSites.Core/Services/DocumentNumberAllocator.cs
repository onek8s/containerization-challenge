using System;
using System.Globalization;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;

namespace Lucerne.ProjectSites.Core.Services
{
    /// <summary>
    /// Allocates document numbers from per-discipline/type counters in the project site's property bag.
    /// The ItemAdding receiver calls this; conflicting updates of the web are retried.
    /// </summary>
    public class DocumentNumberAllocator
    {
        private readonly ISiteSession _site;
        private readonly string _originator;

        public DocumentNumberAllocator(ISiteSession site, string originator = DocumentNumber.DefaultOriginator)
        {
            _site = site;
            _originator = originator;
            MaxAttempts = 5;
        }

        public int MaxAttempts { get; set; }

        public DocumentNumber Next(string projectNumber, string discipline, string type)
        {
            var key = DocumentNumber.CounterKeyFor(discipline, type);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    int current;
                    int.TryParse(_site.GetProperty(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out current);
                    var next = current + 1;
                    _site.SetProperty(key, next.ToString(CultureInfo.InvariantCulture));
                    _site.Commit();
                    return new DocumentNumber(projectNumber, _originator, discipline, type, next);
                }
                catch (SharePointException ex) when (ex.Transient && attempt < MaxAttempts)
                {
                    System.Threading.Thread.Sleep(50 * attempt);
                }
            }
        }

        /// <summary>Raises the counter when a document is uploaded with an existing number (imported from an older site).</summary>
        public void Observe(DocumentNumber number)
        {
            int current;
            int.TryParse(_site.GetProperty(number.CounterKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out current);
            if (number.Sequence > current)
            {
                _site.SetProperty(number.CounterKey, number.Sequence.ToString(CultureInfo.InvariantCulture));
                _site.Commit();
            }
        }
    }
}
