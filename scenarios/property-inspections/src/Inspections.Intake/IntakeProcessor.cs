using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Intake
{
    /// <summary>
    /// Files in the intake folder are named {claim number}_{anything}.{ext}, e.g. HO-2024-004512_roof-north.jpg.
    /// An optional {file}.txt next to the photo holds its caption. Imported files move to Processed\{yyyy-MM-dd},
    /// files that cannot be matched or decoded move to Rejected with a .reason.txt.
    /// </summary>
    public sealed class IntakeProcessor
    {
        private readonly string intake;
        private readonly TimeSpan settle;
        private readonly PhotoStore store;
        private readonly InspectionRepository inspections;

        public IntakeProcessor(string intakeFolder, TimeSpan settle, PhotoStore store, InspectionRepository inspections)
        {
            intake = intakeFolder;
            this.settle = settle;
            this.store = store;
            this.inspections = inspections;
        }

        public void EnsureFolders()
        {
            Directory.CreateDirectory(intake);
            Directory.CreateDirectory(Path.Combine(intake, "Processed"));
            Directory.CreateDirectory(Path.Combine(intake, "Rejected"));
        }

        public void ScanOnce()
        {
            var files = store.Share.Run(() => new DirectoryInfo(intake).GetFiles()
                .Where(f => PhotoStorage.IsAcceptedFileName(f.Name))
                .Where(f => DateTime.UtcNow - f.LastWriteTimeUtc >= settle)
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList());

            foreach (var file in files)
            {
                Import(file);
            }
        }

        private void Import(FileInfo file)
        {
            var underscore = file.Name.IndexOf('_');
            string claim;
            if (underscore <= 0 || !ClaimNumber.TryNormalize(file.Name.Substring(0, underscore), out claim))
            {
                Reject(file, "The file name does not start with a claim number followed by '_'.");
                return;
            }

            var inspection = inspections.ForClaim(claim)
                .Where(i => i.Status < InspectionStatus.ReportQueued)
                .OrderByDescending(i => i.InspectedOn)
                .FirstOrDefault();
            if (inspection == null)
            {
                Reject(file, "No open inspection exists for claim " + claim + ".");
                return;
            }

            byte[] content;
            string caption = null;
            try
            {
                content = store.Share.Run(() =>
                {
                    var captionFile = file.FullName + ".txt";
                    if (File.Exists(captionFile))
                    {
                        caption = File.ReadAllText(captionFile).Trim();
                    }
                    return File.ReadAllBytes(file.FullName);
                });
            }
            catch (IOException ex)
            {
                // Still being written by the sync client; picked up on the next scan.
                Trace.TraceInformation("Skipping {0}: {1}", file.Name, ex.Message);
                return;
            }

            if (content.LongLength > PhotoStorage.MaxUploadBytes)
            {
                Reject(file, "The file is larger than 15 MB.");
                return;
            }

            try
            {
                var photo = store.Add(inspection, file.Name.Substring(underscore + 1), Watermark.Caption(caption, 400), content, "intake");
                inspections.Audit(inspection.Id, "Photo imported", "intake", file.Name);
                Move(file, Path.Combine(intake, "Processed", DateTime.Now.ToString("yyyy-MM-dd")));
                Trace.TraceInformation("Imported {0} as photo {1} of inspection {2}.", file.Name, photo.Id, inspection.Id);
            }
            catch (InvalidPhotoException ex)
            {
                Reject(file, ex.Message);
            }
        }

        private void Reject(FileInfo file, string reason)
        {
            InspectionsLog.Warning(EventId.PhotoRejected, "Intake file {0} rejected: {1}", file.Name, reason);
            var folder = Path.Combine(intake, "Rejected");
            Move(file, folder);
            store.Share.Run(() => File.WriteAllText(Path.Combine(folder, file.Name + ".reason.txt"), reason));
        }

        private void Move(FileInfo file, string folder)
        {
            store.Share.Run(() =>
            {
                Directory.CreateDirectory(folder);
                var target = Path.Combine(folder, file.Name);
                if (File.Exists(target))
                {
                    target = Path.Combine(folder, Path.GetFileNameWithoutExtension(file.Name) + "-" + DateTime.Now.ToString("HHmmssfff") + file.Extension);
                }
                File.Move(file.FullName, target);
                var captionFile = file.FullName + ".txt";
                if (File.Exists(captionFile))
                {
                    File.Move(captionFile, target + ".txt");
                }
            });
        }
    }
}
