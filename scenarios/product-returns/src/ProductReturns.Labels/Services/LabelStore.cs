using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace ProductReturns.Labels.Services
{
    /// <summary>Stores label PDFs on the returns share. The API serves them from the same path.</summary>
    public class LabelStore
    {
        private readonly LabelOptions _options;

        public LabelStore(IOptions<LabelOptions> options)
        {
            _options = options.Value;
        }

        public string Save(string rmaNumber, DateTime createdUtc, byte[] pdf)
        {
            var folder = Path.Combine(_options.SharePath, createdUtc.ToString("yyyy"), createdUtc.ToString("MM"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, rmaNumber + ".pdf");
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, pdf);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);
            return Path.GetFullPath(path);
        }
    }
}
