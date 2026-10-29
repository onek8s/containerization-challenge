using System;
using System.IO;

namespace Contoso.Purchasing.Api.Infrastructure
{
    /// <summary>
    /// Quotes and other attachments are kept on the purchasing file share, one folder per year and month.
    /// </summary>
    public class AttachmentStore
    {
        private readonly string _root;

        public AttachmentStore(string root)
        {
            _root = root;
        }

        public string Save(Stream content, string originalFileName, DateTime now)
        {
            var folder = now.ToString("yyyy") + "\\" + now.ToString("MM");
            var storedName = folder + "\\" + Guid.NewGuid().ToString("N") + Path.GetExtension(originalFileName).ToLowerInvariant();
            var fullPath = Path.Combine(_root, storedName);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
            {
                content.CopyTo(file);
            }

            return storedName;
        }

        public Stream Open(string storedName)
        {
            return new FileStream(Path.Combine(_root, storedName), FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
