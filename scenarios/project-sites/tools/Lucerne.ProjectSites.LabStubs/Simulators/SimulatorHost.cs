using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Lucerne.ProjectSites.Contracts.DocumentControl;
using Lucerne.ProjectSites.Contracts.Erp;
using Lucerne.ProjectSites.Contracts.Hr;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.LabStubs.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Lucerne.ProjectSites.LabStubs.Simulators
{
    /// <summary>
    /// Hosts the three external systems on one Kestrel port:
    ///   /ProjectAccounting/ProjectService.svc   ERP (SOAP 1.1, basicHttp)
    ///   /hrapi/api/v2/employees                  HR (REST, X-Api-Key)
    ///   /doccontrol/oauth/token, /doccontrol/api/v1/submissions   document control (OAuth2 client credentials)
    /// </summary>
    public sealed class SimulatorHost : IAsyncDisposable
    {
        public const string HrApiKey = "lab-hr-key";
        public const string DocControlClientId = "project-sites";
        public const string DocControlSecret = "lab-secret";

        private readonly WebApplication _app;

        public SimulatorHost(SeedData seed, int port)
        {
            Seed = seed;
            Port = port;
            Erp = new ErpSimulator(seed);

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
            builder.Services.AddServiceModelServices();
            builder.Services.AddSingleton(Erp);
            _app = builder.Build();

            _app.UseServiceModel(sm =>
            {
                sm.AddService<ErpSimulator>();
                sm.AddServiceEndpoint<ErpSimulator, IErpProjectService>(new CoreWCF.BasicHttpBinding(), "/ProjectAccounting/ProjectService.svc");
            });

            _app.MapGet("/hrapi/api/v2/employees", (Func<HttpContext, IResult>)Employees);
            _app.MapPost("/doccontrol/oauth/token", (Func<HttpContext, Task<IResult>>)Token);
            _app.MapPost("/doccontrol/api/v1/submissions", (Func<HttpContext, Task<IResult>>)Submit);
            _app.MapGet("/doccontrol/api/v1/submissions/{id}", (Func<HttpContext, string, IResult>)Status);
        }

        public SeedData Seed { get; }
        public int Port { get; }
        public ErpSimulator Erp { get; }
        public ConcurrentDictionary<string, SimSubmission> Submissions { get; } = new ConcurrentDictionary<string, SimSubmission>();
        public string BaseUrl => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
        public int HrRequests { get; private set; }

        public Task StartAsync() => _app.StartAsync();

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private IResult Employees(HttpContext http)
        {
            HrRequests++;
            if (http.Request.Headers["X-Api-Key"] != HrApiKey)
            {
                return Results.StatusCode(401);
            }

            int page, size;
            int.TryParse(http.Request.Query["page"], out page);
            int.TryParse(http.Request.Query["pageSize"], out size);
            page = Math.Max(1, page);
            size = size <= 0 ? 100 : Math.Min(size, 200);
            DateTime since;
            var filtered = Seed.Employees.AsEnumerable();
            if (DateTime.TryParse(http.Request.Query["modifiedSince"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out since))
            {
                filtered = filtered.Where(e => e.LastModified > since);
            }

            var all = filtered.OrderBy(e => e.PersonnelNumber).ToList();
            var result = new HrEmployeePage { Items = all.Skip((page - 1) * size).Take(size).ToList(), Page = page, PageSize = size, Total = all.Count };
            return Results.Text(JsonConvert.SerializeObject(result), "application/json");
        }

        private static async Task<IResult> Token(HttpContext http)
        {
            var form = await http.Request.ReadFormAsync();
            if (form["grant_type"] != "client_credentials" || form["client_id"] != DocControlClientId || form["client_secret"] != DocControlSecret)
            {
                return Results.Json(new { error = "invalid_client" }, statusCode: 401);
            }

            return Results.Text(JsonConvert.SerializeObject(new { access_token = "lab-token-" + Guid.NewGuid().ToString("N"), token_type = "Bearer", expires_in = 3600 }), "application/json");
        }

        private async Task<IResult> Submit(HttpContext http)
        {
            if (!Authorised(http))
            {
                return Results.StatusCode(401);
            }

            var form = await http.Request.ReadFormAsync();
            var metadata = JsonConvert.DeserializeObject<DocumentSubmission>(form["metadata"]);
            var file = form.Files["file"];
            if (metadata == null || file == null)
            {
                return Results.BadRequest("metadata and file are required");
            }

            byte[] content;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                content = ms.ToArray();
            }

            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            DocumentNumber parsed;
            var sub = new SimSubmission
            {
                Id = "DC-" + (Submissions.Count + 1001).ToString(CultureInfo.InvariantCulture),
                Metadata = metadata,
                Size = content.Length,
                // Registration rules of the document control desk: valid number, matching checksum.
                WillReject = !DocumentNumber.TryParse(metadata.DocumentNumber, out parsed) || hash != metadata.Sha256,
                Polls = 0
            };
            Submissions[sub.Id] = sub;
            var receipt = new SubmissionReceipt { SubmissionId = sub.Id, Status = SubmissionStates.Received, ReceivedAt = Seed.NowUtc };
            return Results.Text(JsonConvert.SerializeObject(receipt), "application/json", statusCode: 201);
        }

        private IResult Status(HttpContext http, string id)
        {
            if (!Authorised(http))
            {
                return Results.StatusCode(401);
            }

            SimSubmission sub;
            if (!Submissions.TryGetValue(id, out sub))
            {
                return Results.NotFound();
            }

            sub.Polls++;
            var status = new SubmissionStatus { SubmissionId = id, UpdatedAt = Seed.NowUtc };
            if (sub.Polls < 2)
            {
                status.Status = SubmissionStates.Checking;
            }
            else if (sub.WillReject)
            {
                status.Status = SubmissionStates.Rejected;
                status.Comment = "Document number does not follow LE-QP-07 or checksum mismatch";
            }
            else
            {
                status.Status = SubmissionStates.Registered;
                status.RegisteredNumber = sub.Metadata.DocumentNumber + "/" + sub.Metadata.Revision;
            }

            return Results.Text(JsonConvert.SerializeObject(status), "application/json");
        }

        private static bool Authorised(HttpContext http)
        {
            var auth = http.Request.Headers["Authorization"].ToString();
            return auth.StartsWith("Bearer lab-token-", StringComparison.Ordinal);
        }
    }

    public class SimSubmission
    {
        public string Id { get; set; }
        public DocumentSubmission Metadata { get; set; }
        public int Size { get; set; }
        public bool WillReject { get; set; }
        public int Polls { get; set; }
    }
}
