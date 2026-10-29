using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.DocumentService.Storage;
using Fabrikam.Housing.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.DocumentService.Intake
{
    public class IntakeOptions
    {
        /// <summary>Shared as \\docs01\Scans$ for the post room multifunction devices.</summary>
        public string WatchFolder { get; set; } = @"D:\Scans\Inbound";
        public string ProcessedFolder { get; set; } = @"D:\Scans\Processed";
        public string FailedFolder { get; set; } = @"D:\Scans\Failed";
        public int SweepMinutes { get; set; } = 5;
    }

    /// <summary>
    /// Files scanned post from the MFDs into the document store. The post room uses the MFD job names
    /// TENANCY_{ref} or PROPERTY_{ref}; the device appends _yyyyMMdd_HHmm. Anything else is stored unlinked
    /// for the housing team to file.
    /// </summary>
    public class IntakeService : BackgroundService
    {
        private static readonly Regex Name = new Regex(@"^(?<kind>TENANCY|PROPERTY)_(?<ref>[TP]\d{5,6})_(?<date>\d{8})_(?<time>\d{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly IntakeOptions _options;
        private readonly DocumentStore _store;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<IntakeService> _logger;
        private readonly BlockingCollection<string> _queue = new BlockingCollection<string>();
        private FileSystemWatcher _watcher;

        public IntakeService(IOptions<IntakeOptions> options, DocumentStore store, IServiceScopeFactory scopes, ILogger<IntakeService> logger)
        {
            _options = options.Value;
            _store = store;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            Directory.CreateDirectory(_options.WatchFolder);
            Directory.CreateDirectory(_options.ProcessedFolder);
            Directory.CreateDirectory(_options.FailedFolder);

            _watcher = new FileSystemWatcher(_options.WatchFolder) { IncludeSubdirectories = false, EnableRaisingEvents = true };
            _watcher.Created += (s, e) => _queue.Add(e.FullPath);
            _watcher.Renamed += (s, e) => _queue.Add(e.FullPath);
            _watcher.Error += (s, e) => _logger.LogError(e.GetException(), "Scan folder watcher failed; the sweep will pick files up");
            _logger.LogInformation("Watching {Folder} for scanned post", _options.WatchFolder);

            var nextSweep = DateTime.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (DateTime.Now >= nextSweep)
                {
                    foreach (var f in Directory.GetFiles(_options.WatchFolder)) _queue.Add(f);
                    nextSweep = DateTime.Now.AddMinutes(_options.SweepMinutes);
                }
                if (_queue.TryTake(out var path, 1000))
                {
                    await ProcessAsync(path, stoppingToken);
                }
            }
            _watcher.Dispose();
        }

        public async Task ProcessAsync(string path, CancellationToken token)
        {
            var name = Path.GetFileName(path);
            if (!File.Exists(path) || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || name.StartsWith("~")) return;

            byte[] content;
            try
            {
                content = await ReadWhenCompleteAsync(path, token);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "{File} is still locked; it will be retried by the sweep", name);
                return;
            }

            try
            {
                if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !IsPdf(content))
                    throw new InvalidDataException("Not a PDF file (scan interrupted or wrong file type)");
                var doc = new NewDocument
                {
                    Category = DocumentCategory.ScannedPost,
                    FileName = name,
                    ContentType = ContentTypeFor(name),
                    UploadedBy = "scan-intake",
                    Title = "Scanned post"
                };
                var m = Name.Match(Path.GetFileNameWithoutExtension(name));
                if (m.Success)
                {
                    if (DateTime.TryParseExact(m.Groups["date"].Value + m.Groups["time"].Value, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scanned))
                    {
                        doc.UploadedAt = scanned;
                        doc.Title = "Scanned post " + scanned.ToString("dd/MM/yyyy HH:mm");
                    }
                    await LinkAsync(doc, m.Groups["kind"].Value.ToUpperInvariant(), m.Groups["ref"].Value.ToUpperInvariant());
                }
                await _store.SaveAsync(doc, content);
                Move(path, Path.Combine(_options.ProcessedFolder, DateTime.Now.ToString("yyyyMMdd")));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not file scanned document {File}", name);
                Move(path, _options.FailedFolder);
            }
        }

        private async Task LinkAsync(NewDocument doc, string kind, string reference)
        {
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                if (kind == "TENANCY")
                {
                    var t = await db.Tenancies.AsNoTracking().Where(x => x.Reference == reference).Select(x => new { x.Id, x.PropertyId }).FirstOrDefaultAsync();
                    if (t == null) { _logger.LogWarning("Scanned document for unknown tenancy {Reference}", reference); return; }
                    doc.TenancyId = t.Id;
                    doc.PropertyId = t.PropertyId;
                    doc.Title += " - " + reference;
                }
                else
                {
                    var p = await db.Properties.AsNoTracking().Where(x => x.Reference == reference).Select(x => (int?)x.Id).FirstOrDefaultAsync();
                    if (p == null) { _logger.LogWarning("Scanned document for unknown property {Reference}", reference); return; }
                    doc.PropertyId = p;
                    doc.Title += " - " + reference;
                }
                doc.Category = DocumentCategory.Correspondence;
            }
        }

        /// <summary>The MFD writes over SMB; wait until the file size is stable and it can be opened exclusively.</summary>
        private static async Task<byte[]> ReadWhenCompleteAsync(string path, CancellationToken token)
        {
            long last = -1;
            for (var i = 0; i < 30; i++)
            {
                var size = new FileInfo(path).Length;
                if (size > 0 && size == last)
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (var ms = new MemoryStream())
                    {
                        await stream.CopyToAsync(ms, 81920, token);
                        return ms.ToArray();
                    }
                }
                last = size;
                await Task.Delay(1000, token);
            }
            throw new IOException("File did not finish writing: " + path);
        }

        private void Move(string path, string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var target = Path.Combine(folder, Path.GetFileName(path));
                if (File.Exists(target)) target = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.ToString("HHmmssfff") + Path.GetExtension(path));
                File.Move(path, target);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Could not move {File} to {Folder}", path, folder);
            }
        }

        private static bool IsPdf(byte[] content) =>
            content.Length > 4 && content[0] == '%' && content[1] == 'P' && content[2] == 'D' && content[3] == 'F';

        public static string ContentTypeFor(string name)
        {
            switch (Path.GetExtension(name).ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".tif":
                case ".tiff": return "image/tiff";
                default: return "application/octet-stream";
            }
        }
    }
}
