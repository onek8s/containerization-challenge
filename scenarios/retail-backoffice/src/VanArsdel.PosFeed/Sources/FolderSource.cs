using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace VanArsdel.PosFeed.Sources
{
    public class FolderSource : IPosFileSource
    {
        private readonly PosFeedOptions _options;

        public FolderSource(IOptions<PosFeedOptions> options)
        {
            _options = options.Value;
            Directory.CreateDirectory(_options.Folder.Inbox);
            Directory.CreateDirectory(_options.Folder.Archive);
            Directory.CreateDirectory(_options.Folder.Error);
        }

        public string Description => "folder " + Path.GetFullPath(_options.Folder.Inbox);

        public Task<IReadOnlyList<PosFile>> ListAsync(CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-_options.MinFileAgeSeconds);
            IReadOnlyList<PosFile> files = new DirectoryInfo(_options.Folder.Inbox)
                .GetFiles(_options.FilePattern)
                .Where(f => f.LastWriteTimeUtc < cutoff)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new PosFile { Name = f.Name, Size = f.Length, Handle = f.FullName })
                .ToList();
            return Task.FromResult(files);
        }

        public Task<Stream> OpenAsync(PosFile file, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new FileStream((string)file.Handle, FileMode.Open, FileAccess.Read, FileShare.Read));

        public Task CompleteAsync(PosFile file, CancellationToken cancellationToken)
        {
            var day = Path.Combine(_options.Folder.Archive, DateTime.Today.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(day);
            MoveReplacing((string)file.Handle, Path.Combine(day, file.Name));
            return Task.CompletedTask;
        }

        public Task FailAsync(PosFile file, string reason, CancellationToken cancellationToken)
        {
            var target = Path.Combine(_options.Folder.Error, file.Name);
            MoveReplacing((string)file.Handle, target);
            File.WriteAllText(target + ".err", DateTime.Now.ToString("s") + " " + reason + Environment.NewLine);
            return Task.CompletedTask;
        }

        private static void MoveReplacing(string from, string to)
        {
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }
    }
}
