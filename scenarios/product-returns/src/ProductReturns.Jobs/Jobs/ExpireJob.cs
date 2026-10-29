using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProductReturns.Core;
using ProductReturns.Data;

namespace ProductReturns.Jobs.Jobs
{
    /// <summary>Closes returns whose parcel never arrived.</summary>
    public class ExpireJob : IJob
    {
        private readonly ReturnsRepository _repository;
        private readonly ReturnPolicyOptions _policy;
        private readonly ILogger<ExpireJob> _logger;

        public ExpireJob(ReturnsRepository repository, ReturnPolicyOptions policy, ILogger<ExpireJob> logger)
        {
            _repository = repository;
            _policy = policy;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var before = DateTime.UtcNow.AddDays(-_policy.ExpireAfterDays);
            var expired = await _repository.AwaitingReceiptAsync(before, cancellationToken);
            foreach (var request in expired)
            {
                request.Status = ReturnStatus.Closed;
                request.Notes.Add(new ReturnNote
                {
                    Author = "jobs",
                    Text = "Closed: parcel not received within " + _policy.ExpireAfterDays + " days.",
                    CreatedUtc = DateTime.UtcNow
                });
            }

            await _repository.SaveAsync(cancellationToken);
            _logger.LogInformation("{Count} returns closed as not received", expired.Count);
        }
    }
}
