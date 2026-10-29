using System;
using System.Collections.Generic;
using System.IO;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Seed;
using Fabrikam.Housing.Hosting;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Api.Tests.Support
{
    /// <summary>
    /// The jobs' services on a freshly seeded SQLite database, without Hangfire. Each test gets its own harness.
    /// </summary>
    public sealed class JobsHarness : IDisposable
    {
        public static readonly DateTime Today = new DateTime(2021, 6, 14);
        private readonly ServiceProvider _provider;

        public JobsHarness(Action<IServiceCollection> configure = null)
        {
            Folder = new TestFolder();
            Clock = new FixedClock(Today.AddHours(7));
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:Housing"] = "Data Source=" + Folder.Sub("housing.db"),
                ["Database:Provider"] = "Sqlite",
                ["Integration:Mode"] = "Simulated",
                ["Integration:SimulatorFolder"] = Folder.Sub("simulators"),
                ["Documents:PdfEngine"] = "Basic",
                ["Jobs:TimeZone"] = "Europe/London",
                ["Jobs:PrintFolder"] = Folder.Sub("print"),
                ["Jobs:HousingBenefitInbound"] = Folder.Sub("hb"),
                ["Jobs:FinanceOutbound"] = Folder.Sub("finance"),
                ["Jobs:BacsArchiveFolder"] = Folder.Sub("bacs-sent")
            }).Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            services.AddHousingCore(Configuration, typeof(Startup).Assembly);
            services.AddHousingJobs(Configuration);
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IDocumentServiceClient>(Documents);
            configure?.Invoke(services);
            _provider = services.BuildServiceProvider();

            using (var db = CreateDbContext())
            {
                db.Database.EnsureCreated();
                new DevelopmentSeeder(db, Today).Seed();
            }
        }

        public TestFolder Folder { get; }
        public FixedClock Clock { get; }
        public IConfiguration Configuration { get; }
        public FakeDocumentServiceClient Documents { get; } = new FakeDocumentServiceClient();

        public string Simulator(string sub) => Folder.Sub("simulators", sub);

        public T Run<T>(Func<T, System.Threading.Tasks.Task> action) where T : class
        {
            using (var scope = _provider.CreateScope())
            {
                var job = scope.ServiceProvider.GetRequiredService<T>();
                action(job).GetAwaiter().GetResult();
                return job;
            }
        }

        public IServiceScope CreateScope() => _provider.CreateScope();

        public HousingDbContext CreateDbContext() =>
            new HousingDbContext(new DbContextOptionsBuilder<HousingDbContext>().UseSqlite("Data Source=" + Folder.Sub("housing.db")).Options);

        public IEnumerable<string> Files(params string[] folder) =>
            Directory.Exists(Folder.Sub(folder)) ? Directory.GetFiles(Folder.Sub(folder), "*", SearchOption.AllDirectories) : new string[0];

        public void Dispose()
        {
            _provider.Dispose();
            Folder.Dispose();
        }
    }
}
