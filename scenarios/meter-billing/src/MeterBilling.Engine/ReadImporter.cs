using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Coho.Billing.Core;
using Coho.Billing.Core.Reads;
using Coho.Billing.Data;

namespace Coho.Billing.Engine
{
    /// <summary>
    /// Imports read files from the inbox share. The handheld docking stations copy route files into
    /// Inbox\Handheld, the AMR collection PC writes Inbox\Amr. Files are moved to Processed\yyyyMM or
    /// Rejected (with a .err text next to them) after import.
    /// </summary>
    public class ReadImporter
    {
        private readonly EngineSettings _settings;
        private readonly BillingDb _db;

        public ReadImporter(EngineSettings settings, BillingDb db)
        {
            _settings = settings;
            _db = db;
        }

        public int ImportPending()
        {
            if (!Directory.Exists(_settings.ReadInbox))
            {
                EngineLog.Warning("Read inbox {0} is not reachable", _settings.ReadInbox);
                return 0;
            }

            var files = Directory.EnumerateFiles(_settings.ReadInbox, "*.*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(f => !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) && !f.Name.StartsWith("~", StringComparison.Ordinal))
                .Where(f => DateTime.Now - f.LastWriteTime >= _settings.FileSettle)
                .OrderBy(f => f.LastWriteTime)
                .ToList();

            var imported = 0;
            HashSet<string> meters = null;
            foreach (var file in files)
            {
                if (!IsComplete(file))
                {
                    continue;
                }

                if (meters == null)
                {
                    meters = _db.GetMeterSerials();
                }

                if (Import(file, meters))
                {
                    imported++;
                }
            }

            return imported;
        }

        private bool Import(FileInfo file, HashSet<string> meters)
        {
            byte[] content = File.ReadAllBytes(file.FullName);
            string hash;
            using (var sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(content)).Replace("-", string.Empty);
            }

            var duplicate = _db.FindImportByHash(hash);
            if (duplicate != null)
            {
                _db.SaveImport(file.Name, ReadSource.Handheld, null, hash, 2, "Same content as " + duplicate.FileName + " imported " + duplicate.ReceivedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), new List<MeterRead>(), 0);
                Move(file, _settings.ReadRejected, "Duplicate of " + duplicate.FileName);
                EngineLog.Event(EventLogEntryType.Warning, 2101, "Read file " + file.Name + " is a duplicate of " + duplicate.FileName);
                return false;
            }

            ReadFile parsed;
            try
            {
                using (var stream = new MemoryStream(content))
                {
                    parsed = ReadFileParsers.Parse(file.Name, stream);
                }
            }
            catch (ReadFileException ex)
            {
                _db.SaveImport(file.Name, ReadSource.Handheld, null, hash, 1, ex.Message, new List<MeterRead>(), 0);
                Move(file, _settings.ReadRejected, ex.Message);
                EngineLog.Event(EventLogEntryType.Warning, 2102, "Read file " + file.Name + " rejected: " + ex.Message);
                return false;
            }

            var unknown = parsed.Reads.Where(r => !meters.Contains(r.MeterSerial)).ToList();
            foreach (var read in unknown)
            {
                parsed.RejectedLines.Add("meter " + read.MeterSerial + " is not registered");
                parsed.Reads.Remove(read);
            }

            var message = parsed.RejectedLines.Count == 0 ? null : string.Join(Environment.NewLine, parsed.RejectedLines);
            _db.SaveImport(file.Name, parsed.Source, parsed.BatchId, hash, 0, message, parsed.Reads, parsed.RejectedLines.Count);

            var archive = Path.Combine(_settings.ReadArchive, DateTime.Today.ToString("yyyyMM", CultureInfo.InvariantCulture));
            Move(file, archive, message);
            EngineLog.Info("Imported {0}: {1} reads, {2} lines not used", file.Name, parsed.Reads.Count, parsed.RejectedLines.Count);
            return true;
        }

        private static bool IsComplete(FileInfo file)
        {
            try
            {
                using (file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                // Still being copied by the docking station.
                return false;
            }
        }

        private static void Move(FileInfo file, string folder, string note)
        {
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, file.Name);
            if (File.Exists(target))
            {
                target = Path.Combine(folder, Path.GetFileNameWithoutExtension(file.Name) + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + file.Extension);
            }

            File.Move(file.FullName, target);
            if (!string.IsNullOrEmpty(note))
            {
                File.WriteAllText(target + ".err", note);
            }
        }
    }
}
