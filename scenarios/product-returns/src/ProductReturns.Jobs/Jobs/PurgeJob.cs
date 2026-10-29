using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Data;
using ProductReturns.Jobs.Infrastructure;

namespace ProductReturns.Jobs.Jobs
{
    /// <summary>Deletes old label PDFs from the share and removes personal data from old returns.</summary>
    public class PurgeJob : IJob
    {
        private readonly ReturnsRepository _repository;
        private readonly JobOptions _options;
        private readonly ILogger<PurgeJob> _logger;

        public PurgeJob(ReturnsRepository repository, IOptions<JobOptions> options, ILogger<PurgeJob> logger)
        {
            _repository = repository;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var deleted = 0;
            var labelLimit = DateTime.UtcNow.AddDays(-_options.LabelRetentionDays);
            if (Directory.Exists(_options.LabelShare))
            {
                foreach (var file in Directory.EnumerateFiles(_options.LabelShare, "*.pdf", SearchOption.AllDirectories))
                {
                    if (File.GetLastWriteTimeUtc(file) < labelLimit)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }

                foreach (var folder in Directory.EnumerateDirectories(_options.LabelShare, "*", SearchOption.AllDirectories))
                {
                    if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                    {
                        Directory.Delete(folder);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Label share {Share} not found", _options.LabelShare);
            }

            var old = await _repository.ClosedBeforeAsync(DateTime.UtcNow.AddDays(-_options.AnonymizeAfterDays), cancellationToken);
            foreach (var request in old)
            {
                request.CustomerName = "(removed)";
                request.CustomerEmail = "removed-" + request.Id + "@invalid";
                request.LabelPath = null;
                foreach (var line in request.Lines)
                {
                    line.CustomerComment = null;
                }

                request.AnonymizedUtc = DateTime.UtcNow;
            }

            await _repository.SaveAsync(cancellationToken);
            _logger.LogInformation("{Files} label files deleted, {Returns} returns anonymized", deleted, old.Count);
        }
    }
}
