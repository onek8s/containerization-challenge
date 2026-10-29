using Litware.DocControl.Documents;
using Litware.DocControl.Platform;

namespace Litware.DocControl.Indexer;

/// <summary>
/// Unpacks DocTrack 5 transmittal archives into a project drop folder so that the normal scan registers
/// the revisions. Entries whose names do not follow the numbering procedure are written to an
/// <c>_unmatched</c> folder for manual handling.
/// </summary>
public static class DocTrackImport
{
    public static int Run(string sourceFolder, string dropFolder, ILogger log)
    {
        var archives = Directory.GetFiles(sourceFolder, "*.dta", SearchOption.AllDirectories);
        log.LogInformation("{Count} DocTrack archives in {Folder}", archives.Length, sourceFolder);

        int extracted = 0, unmatched = 0, failed = 0;
        foreach (var path in archives.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var archive = new DocTrackArchive(path);
                foreach (var entry in archive.Entries())
                {
                    var name = Path.GetFileName(entry.Name);
                    var matched = RevisionFileName.TryParse(name, out _, out _);
                    var target = matched
                        ? Path.Combine(dropFolder, name)
                        : Path.Combine(dropFolder, "_unmatched", Path.GetFileNameWithoutExtension(path), name);
                    if (File.Exists(target)) continue;

                    archive.Extract(entry, target);
                    File.SetLastWriteTimeUtc(target, entry.ModifiedUtc);
                    if (matched) extracted++; else unmatched++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                log.LogError(ex, "{Archive} could not be read", path);
            }
        }

        log.LogInformation("{Extracted} files extracted to {Drop}, {Unmatched} unmatched, {Failed} archives failed", extracted, dropFolder, unmatched, failed);
        return failed == 0 ? 0 : 2;
    }
}
