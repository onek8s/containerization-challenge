using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace VanArsdel.Scheduler.Jobs
{
    /// <summary>
    /// Every scheduled job is a POST to one of the services, authenticated with the scheduler's
    /// client credentials. The work itself stays in the owning service.
    /// </summary>
    [DisallowConcurrentExecution]
    public class ServiceCallJob : IJob
    {
        public const string ServiceKey = "service";
        public const string PathKey = "path";

        private readonly IHttpClientFactory _clients;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ServiceCallJob> _logger;

        public ServiceCallJob(IHttpClientFactory clients, IConfiguration configuration, ILogger<ServiceCallJob> logger)
        {
            _clients = clients;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var service = context.MergedJobDataMap.GetString(ServiceKey);
            var path = context.MergedJobDataMap.GetString(PathKey);
            var baseUrl = _configuration["Services:" + service] ?? throw new InvalidOperationException("Services:" + service + " is not configured");
            var watch = Stopwatch.StartNew();

            var client = _clients.CreateClient("services");
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/" + path.TrimStart('/'));
            request.Headers.Add("X-Correlation-ID", "job-" + context.FireInstanceId);
            try
            {
                using var response = await client.SendAsync(request, context.CancellationToken);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    // Quartz logs and the next fire tries again; a misfire is better than a hammering loop.
                    throw new JobExecutionException($"{service} {path} returned {(int)response.StatusCode}: {Trim(body)}");
                }
                _logger.LogInformation("Job {Job} {Service} {Path} done in {Elapsed} ms: {Result}",
                    context.JobDetail.Key.Name, service, path, watch.ElapsedMilliseconds, Trim(body));
            }
            catch (HttpRequestException ex)
            {
                throw new JobExecutionException($"{service} unreachable: {ex.Message}", ex);
            }
        }

        private static string Trim(string body) => body == null || body.Length <= 300 ? body : body.Substring(0, 300) + "...";
    }

    public class JobDefinition
    {
        public string Name { get; set; }
        public string Service { get; set; }
        public string Path { get; set; }
        public string Cron { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
