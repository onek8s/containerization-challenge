using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BatchRelease.Core.Intake;
using BatchRelease.Core.Model;
using BatchRelease.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BatchRelease.Tests;

public class ApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Seed();
    }

    private HttpClient Client(string? key)
    {
        var client = _factory.CreateClient();
        if (key is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static BatchSubmission Submission(string batchNumber = "T2600418", string site = "TKU") => new()
    {
        MesReference = "MES-" + batchNumber,
        BatchNumber = batchNumber,
        ProductCode = "PRX-0450",
        SiteCode = site,
        ManufacturedOn = new DateOnly(2026, 9, 14),
        Quantity = 412500,
        Unit = "TAB",
        Steps = { new StepSubmission { Sequence = 10, Operation = "Dispensing", Status = StepStatus.Complete, PerformedBy = "mlahti", VerifiedBy = "jkoski" } },
        Results = { new ResultSubmission { TestCode = "ASSAY", TestName = "Assay", LowerLimit = 95.0m, UpperLimit = 105.0m, Value = 94.94m, Unit = "%", Outcome = ResultOutcome.Pass } },
        Deviations = { new DeviationSubmission { Number = "DEV-26-0112", Title = "Room pressure alarm", Classification = DeviationClass.Minor, Status = DeviationStatus.Open } },
    };

    [Fact]
    public async Task Requires_an_api_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).PostAsJsonAsync("/api/v1/batches", Submission(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("br_mes0tku1_wrong").PostAsJsonAsync("/api/v1/batches", Submission(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(ApiFactory.ErpKey).PostAsJsonAsync("/api/v1/batches", Submission(), Json)).StatusCode);
    }

    [Fact]
    public async Task Submits_and_resubmits_a_batch()
    {
        var mes = Client(ApiFactory.MesKey);
        var created = await mes.PostAsJsonAsync("/api/v1/batches", Submission(), Json);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var update = Submission();
        update.Results[0].Value = 99.1m;
        update.Deviations[0].Status = DeviationStatus.Closed;
        Assert.Equal(HttpStatusCode.OK, (await mes.PostAsJsonAsync("/api/v1/batches", update, Json)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BatchReleaseContext>();
        var batch = await db.Batches.Include(b => b.Results).Include(b => b.Deviations).SingleAsync(b => b.BatchNumber == "T2600418");
        Assert.Equal(new DateOnly(2029, 8, 31), batch.ExpiresOn);
        Assert.Equal(ResultOutcome.Pass, Assert.Single(batch.Results).Outcome);
        Assert.Equal(DeviationStatus.Closed, Assert.Single(batch.Deviations).Status);
    }

    [Fact]
    public async Task Result_outcome_is_evaluated_on_intake()
    {
        await Client(ApiFactory.MesKey).PostAsJsonAsync("/api/v1/batches", Submission("T2600419"), Json);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BatchReleaseContext>();
        var result = await db.LabResults.SingleAsync(r => r.BatchId == db.Batches.Single(b => b.BatchNumber == "T2600419").Id);
        Assert.Equal(ResultOutcome.Fail, result.Outcome);
    }

    [Fact]
    public async Task Rejects_invalid_submissions()
    {
        var mes = Client(ApiFactory.MesKey);

        var bad = Submission("bad");
        Assert.Equal(HttpStatusCode.BadRequest, (await mes.PostAsJsonAsync("/api/v1/batches", bad, Json)).StatusCode);

        var withdrawn = Submission("T2600420");
        withdrawn.ProductCode = "PRX-0900";
        Assert.Equal(HttpStatusCode.BadRequest, (await mes.PostAsJsonAsync("/api/v1/batches", withdrawn, Json)).StatusCode);

        var otherSite = Submission("O2600001", "OUL");
        var response = await mes.PostAsJsonAsync("/api/v1/batches", otherSite, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("site TKU", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Record_is_frozen_after_review()
    {
        var mes = Client(ApiFactory.MesKey);
        await mes.PostAsJsonAsync("/api/v1/batches", Submission("T2600421"), Json);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BatchReleaseContext>();
            var batch = await db.Batches.SingleAsync(b => b.BatchNumber == "T2600421");
            batch.Status = BatchStatus.Reviewed;
            await db.SaveChangesAsync();
        }

        var response = await mes.PostAsJsonAsync("/api/v1/batches", Submission("T2600421"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("can no longer be changed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Forwards_documents_to_the_signing_host()
    {
        var mes = Client(ApiFactory.MesKey);
        await mes.PostAsJsonAsync("/api/v1/batches", Submission("T2600422"), Json);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("ExecutedBatchRecord"), "kind");
        var file = new ByteArrayContent("%PDF-1.7 executed batch record"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "T2600422-ebr.pdf");

        var response = await mes.PostAsync("/api/v1/batches/T2600422/documents", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(_factory.Signing.Stored, s => s.FileName == "T2600422-ebr.pdf");
        Assert.Equal("MES Turku", stored.By);

        using var wrongKind = new MultipartFormDataContent { { new StringContent("Photo"), "kind" }, { new ByteArrayContent(new byte[] { 1 }), "file", "x.jpg" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await mes.PostAsync("/api/v1/batches/T2600422/documents", wrongKind)).StatusCode);
    }

    [Fact]
    public async Task Erp_reads_status_and_certificate()
    {
        await Client(ApiFactory.MesKey).PostAsJsonAsync("/api/v1/batches", Submission("T2600423"), Json);
        var erp = Client(ApiFactory.ErpKey);

        var summary = await erp.GetFromJsonAsync<JsonElement>("/api/v1/batches/T2600423");
        Assert.Equal("Received", summary.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await erp.GetAsync("/api/v1/batches/T2600423/certificate")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BatchReleaseContext>();
            var batch = await db.Batches.SingleAsync(b => b.BatchNumber == "T2600423");
            batch.Status = BatchStatus.Released;
            batch.CertificateNumber = "RC-TKU-2026-00042";
            batch.DispositionUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        _factory.Signing.Certificates["RC-TKU-2026-00042"] = new byte[] { 0x30, 0x82 };

        var cert = await erp.GetAsync("/api/v1/batches/T2600423/certificate");
        Assert.Equal(HttpStatusCode.OK, cert.StatusCode);
        Assert.Equal("application/pkcs7-mime", cert.Content.Headers.ContentType?.MediaType);

        var dispositions = await erp.GetFromJsonAsync<JsonElement>("/api/v1/dispositions");
        Assert.Contains(dispositions.EnumerateArray(), d => d.GetProperty("batchNumber").GetString() == "T2600423");

        // A client limited to another site does not see the batch.
        Assert.Equal(HttpStatusCode.NotFound, (await Client(ApiFactory.OtherSiteKey).GetAsync("/api/v1/batches/T2600423")).StatusCode);
    }

    [Fact]
    public async Task Lab_sample_is_accepted_and_passes_review_checks()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "samples", "T2600418.json"));
        var response = await Client(ApiFactory.MesKey).PostAsync("/api/v1/batches", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var batch = await scope.ServiceProvider.GetRequiredService<BatchQueries>().GetByNumberAsync("T2600418");
        Assert.NotNull(batch);
        Assert.All(batch!.Results, r => Assert.Equal(ResultOutcome.Pass, r.Outcome));
        var review = new Core.Rules.DispositionRules(TestData.Clock()).Assess(batch, DecisionKind.Review, TestData.ReviewerSid);
        Assert.True(review.CanProceed, string.Join("; ", review.Findings));
    }
}
