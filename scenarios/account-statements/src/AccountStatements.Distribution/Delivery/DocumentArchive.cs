using System;
using System.IO;

namespace Woodgrove.Statements.Distribution.Delivery
{
    /// <summary>Archive of every distributed document on the archive share, by year and month of the run.</summary>
    public class DocumentArchive
    {
        readonly string _root;

        public DocumentArchive(string root)
        {
            _root = root;
        }

        public string Store(DateTime runDate, int runId, string fileName, byte[] content)
        {
            var folder = Path.Combine(_root, runDate.ToString("yyyy"), runDate.ToString("MM"), "run" + runId.ToString("000000"));
            return ShareWriter.Write(folder, fileName, content, overwrite: true);
        }

        public byte[] Load(string archivePath)
        {
            if (string.IsNullOrEmpty(archivePath)) throw new FileNotFoundException("The delivery has no archive copy.");
            return File.ReadAllBytes(archivePath);
        }
    }
}
