using Microsoft.Extensions.Options;
using VanArsdel.PosFeed.Sources;
using Xunit;

namespace VanArsdel.PosFeed.Tests;

public sealed class FolderSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "posfeed-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FolderSource _source;

    public FolderSourceTests()
    {
        var options = new PosFeedOptions
        {
            MinFileAgeSeconds = 0,
            Folder = new FolderSourceOptions
            {
                Inbox = Path.Combine(_root, "inbox"),
                Archive = Path.Combine(_root, "archive"),
                Error = Path.Combine(_root, "error"),
            },
        };
        Directory.CreateDirectory(options.Folder.Inbox);
        Directory.CreateDirectory(options.Folder.Error);
        _source = new FolderSource(Options.Create(options));
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void Drop(string name, DateTime? written = null)
    {
        var path = Path.Combine(_root, "inbox", name);
        File.WriteAllText(path, "H;FI012;20261029;001;EUR\n");
        File.SetLastWriteTimeUtc(path, written ?? DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Lists_matching_files_in_name_order()
    {
        Drop("POS_FI012_20261029_002.txt");
        Drop("POS_FI012_20261029_001.txt");
        Drop("readme.txt");

        var files = await _source.ListAsync(CancellationToken.None);

        Assert.Equal(new[] { "POS_FI012_20261029_001.txt", "POS_FI012_20261029_002.txt" }, files.Select(f => f.Name));
    }

    [Fact]
    public async Task Completed_file_goes_to_the_archive_folder_of_the_day()
    {
        Drop("POS_FI012_20261029_001.txt");
        var file = (await _source.ListAsync(CancellationToken.None)).Single();

        await _source.CompleteAsync(file, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_root, "archive", DateTime.Today.ToString("yyyy-MM-dd"), file.Name)));
        Assert.Empty(await _source.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Failed_file_goes_to_error_with_the_reason_next_to_it()
    {
        Drop("POS_FI012_20261029_001.txt");
        var file = (await _source.ListAsync(CancellationToken.None)).Single();

        await _source.FailAsync(file, "Trailer record missing", CancellationToken.None);

        var target = Path.Combine(_root, "error", file.Name);
        Assert.True(File.Exists(target));
        Assert.Contains("Trailer record missing", File.ReadAllText(target + ".err"));
    }
}
