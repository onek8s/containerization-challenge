using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductReturns.Core;
using ProductReturns.Data;
using ProductReturns.Labels.Carriers;
using ProductReturns.Labels.Services;
using Xunit;

namespace ProductReturns.Tests
{
    public class LabelProcessorTests
    {
        private class FailingCarrier : ICarrierClient
        {
            public bool Permanent { get; set; }

            public Task<CarrierLabel> CreateReturnLabelAsync(LabelRequest request, CancellationToken cancellationToken) =>
                throw new CarrierException("Postal code invalid", Permanent);
        }

        private static (LabelProcessor Processor, LabelOptions Options) Create(ReturnsDbContext db, string folder, ICarrierClient carrier, int quota = 10)
        {
            var labelOptions = new LabelOptions
            {
                SharePath = Path.Combine(folder, "labels"),
                StateFolder = Path.Combine(folder, "state"),
                DailyQuota = quota
            };
            var mailOptions = new MailOptions
            {
                PickupDirectory = Path.Combine(folder, "mail"),
                TemplateFolder = Path.Combine(AppContext.BaseDirectory, "Templates"),
                LabelTemplate = "returnlabel.html"
            };
            var environment = new HostingEnvironment { ContentRootPath = AppContext.BaseDirectory, EnvironmentName = "Development" };
            var options = Options.Create(labelOptions);
            var processor = new LabelProcessor(new ReturnsRepository(db), carrier, new LabelStore(options),
                new LabelMailer(Options.Create(mailOptions), environment), new CarrierQuota(options), options, NullLogger<LabelProcessor>.Instance);
            return (processor, labelOptions);
        }

        private static async Task<ReturnRequest> Approved(ReturnsDbContext db)
        {
            var request = new ReturnRequest
            {
                OrderNumber = "SO-1048377",
                CustomerName = "Jonas Peeters",
                CustomerEmail = "jonas.peeters@example.com",
                CreatedUtc = DateTime.UtcNow,
                DecidedUtc = DateTime.UtcNow,
                Status = ReturnStatus.Approved
            };
            request.Lines.Add(new ReturnLine { OrderLineNumber = 1, Sku = "WWI-20310", Quantity = 1, UnitPrice = 189m, ReasonCode = "DAMAGED" });
            await new ReturnsRepository(db).CreateAsync(request);
            return request;
        }

        [Fact]
        public async Task Issues_label_stores_pdf_and_mails_customer()
        {
            using var database = new TestDatabase();
            using var db = database.CreateContext();
            var request = await Approved(db);
            var (processor, _) = Create(db, database.Folder, new FileCarrierClient());

            Assert.Equal(1, await processor.RunOnceAsync(CancellationToken.None));
            Assert.Equal(ReturnStatus.LabelIssued, request.Status);
            Assert.StartsWith("DEV", request.TrackingNumber);
            Assert.True(File.Exists(request.LabelPath));
            Assert.StartsWith("%PDF-1.4", File.ReadAllText(request.LabelPath).Substring(0, 8));
            var mail = Directory.GetFiles(Path.Combine(database.Folder, "mail"), "*.eml").Single();
            Assert.Contains(request.RmaNumber, File.ReadAllText(mail));
            Assert.Equal(0, await processor.RunOnceAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Daily_quota_stops_the_run()
        {
            using var database = new TestDatabase();
            using var db = database.CreateContext();
            await Approved(db);
            await Approved(db);
            var (processor, options) = Create(db, database.Folder, new FileCarrierClient(), quota: 1);

            Assert.Equal(1, await processor.RunOnceAsync(CancellationToken.None));
            Assert.Equal(0, await processor.RunOnceAsync(CancellationToken.None));
            Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd") + ";1", File.ReadAllText(Path.Combine(options.StateFolder, "quota.txt")));
        }

        [Fact]
        public async Task Carrier_errors_are_recorded_and_permanent_errors_stop_retries()
        {
            using var database = new TestDatabase();
            using var db = database.CreateContext();
            var request = await Approved(db);
            var carrier = new FailingCarrier();
            var (processor, options) = Create(db, database.Folder, carrier);

            Assert.Equal(0, await processor.RunOnceAsync(CancellationToken.None));
            Assert.Equal(1, request.LabelAttempts);
            Assert.Equal("Postal code invalid", request.LabelError);
            Assert.Equal(ReturnStatus.Approved, request.Status);

            carrier.Permanent = true;
            await processor.RunOnceAsync(CancellationToken.None);
            Assert.Equal(options.MaxAttempts, request.LabelAttempts);
            Assert.Empty(await new ReturnsRepository(db).PendingLabelsAsync(options.MaxAttempts, 10));
            Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd") + ";0", File.ReadAllText(Path.Combine(options.StateFolder, "quota.txt")));
        }
    }
}
