using ExpenseClaims.Jobs.Cards;
using Microsoft.Extensions.Options;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>Imports the card company's transaction files. Rows that cannot be matched are written next to the file in Failed.</summary>
[DisallowConcurrentExecution]
public sealed class CardTransactionImportJob : IJob
{
    private readonly CardTransactionImporter _importer;
    private readonly CardImportOptions _options;
    private readonly ILogger<CardTransactionImportJob> _logger;

    public CardTransactionImportJob(CardTransactionImporter importer, IOptions<CardImportOptions> options, ILogger<CardTransactionImportJob> logger)
    {
        _importer = importer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var inbound = new DirectoryInfo(_options.InboundFolder);
        if (!inbound.Exists)
        {
            _logger.LogWarning("Card inbound folder {Folder} does not exist", inbound.FullName);
            return;
        }

        foreach (var file in inbound.GetFiles(_options.FilePattern).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            try
            {
                IReadOnlyList<CardTransactionRecord> records;
                await using (var stream = file.OpenRead())
                    records = CardTransactionImporter.Read(stream, _options.Culture);
                var result = await _importer.ImportAsync(records, file.Name, context.CancellationToken);
                _logger.LogInformation("Card file {File}: {Imported} imported, {Duplicates} already known, {Rejected} rejected",
                    file.Name, result.Imported, result.Duplicates, result.Rejected.Count);

                if (result.Rejected.Count > 0)
                {
                    var failed = Directory.CreateDirectory(Path.Combine(inbound.FullName, "Failed"));
                    await File.WriteAllLinesAsync(Path.Combine(failed.FullName, file.Name + ".rejected.txt"), result.Rejected, context.CancellationToken);
                }
                Move(file, "Processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Card file {File} could not be imported", file.Name);
                Move(file, "Failed");
            }
        }
    }

    private static void Move(FileInfo file, string folder)
    {
        var target = Directory.CreateDirectory(Path.Combine(file.DirectoryName!, folder));
        file.MoveTo(Path.Combine(target.FullName, file.Name), overwrite: true);
    }
}
