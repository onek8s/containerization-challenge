using System;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Payments;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Reads the Paystream events API in case a webhook delivery was missed (the contractor API is the webhook receiver).
    /// The position is kept in the Counters table as minutes since 1 January 2020.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(300)]
    public class PaymentsSyncJob : IHousingJob
    {
        public const string CursorName = "PaystreamEventsCursor";
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1);

        private readonly HousingDbContext _db;
        private readonly IPaymentsGateway _payments;
        private readonly PaystreamEventProcessor _processor;
        private readonly IClock _clock;
        private readonly ILogger<PaymentsSyncJob> _logger;

        public PaymentsSyncJob(HousingDbContext db, IPaymentsGateway payments, PaystreamEventProcessor processor, IClock clock, ILogger<PaymentsSyncJob> logger)
        {
            _db = db;
            _payments = payments;
            _processor = processor;
            _clock = clock;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var cursor = await _db.Counters.FindAsync(CursorName);
            var since = cursor == null ? _clock.Now.AddDays(-1) : Epoch.AddMinutes(cursor.Value).AddMinutes(-30);
            var started = _clock.Now;

            var events = await _payments.ListEventsAsync(since);
            var processed = 0;
            foreach (var e in events)
            {
                if (await _processor.ProcessAsync(e, "poll")) processed++;
            }

            var value = (int)(started - Epoch).TotalMinutes;
            if (cursor == null) _db.Counters.Add(new Counter { Name = CursorName, Value = value });
            else cursor.Value = value;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Paystream events since {Since:g}: {Count} read, {Processed} new", since, events.Count, processed);
        }
    }
}
