using System.Net;
using System.Net.Http.Json;
using ContosoFoods.TradeRebates.Api;
using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TradeRebates.Tests;

public sealed class FakeApiStore : IDistributorApiStore
{
    public static readonly string KespoKey = "tr_" + new string('K', 40);
    public static readonly string MetroKey = "tr_" + new string('M', 40);

    public List<(int DistributorId, int Period, IReadOnlyList<ClaimLineInput> Lines)> Submitted { get; } = [];
    private readonly Dictionary<int, ClaimDetail> _claims = [];

    public Task<ApiClientInfo?> ResolveApiClientAsync(byte[] keyHash)
    {
        ApiClientInfo? info = null;
        if (keyHash.SequenceEqual(ApiKeys.Hash(KespoKey)))
            info = new ApiClientInfo { ApiClientId = 1, DistributorId = 1, DistributorCode = "KESPRO", DistributorName = "Kespro" };
        else if (keyHash.SequenceEqual(ApiKeys.Hash(MetroKey)))
            info = new ApiClientInfo { ApiClientId = 2, DistributorId = 3, DistributorCode = "METRO", DistributorName = "Metro" };
        return Task.FromResult(info);
    }

    public Task<int> SubmitClaimAsync(int distributorId, int period, string? reference, string submittedBy, IReadOnlyList<ClaimLineInput> lines)
    {
        if (period == 209901)
            throw new RebateRuleException(50001, "Distributor is not active.");
        Submitted.Add((distributorId, period, lines));
        int id = 1000 + Submitted.Count;
        _claims[id] = new ClaimDetail
        {
            Header = new ClaimHeader { ClaimId = id, DistributorId = distributorId, Period = period, Status = "V", LineCount = lines.Count, TotalAmount = lines.Sum(l => l.NetAmount), ReceivedBy = submittedBy },
            Lines = lines.Select((l, i) => new ClaimLineRow { LineNumber = i + 1, InvoiceNo = l.InvoiceNo, Gtin = l.Gtin, Quantity = l.Quantity, NetAmount = l.NetAmount, LineStatus = "K" }).ToList()
        };
        return Task.FromResult(id);
    }

    public Task<ClaimDetail?> GetClaimAsync(int claimId) => Task.FromResult(_claims.GetValueOrDefault(claimId));

    public Task<IReadOnlyList<DistributorClaim>> ListClaimsAsync(int distributorId, int? period) =>
        Task.FromResult<IReadOnlyList<DistributorClaim>>(_claims.Values.Where(c => c.Header.DistributorId == distributorId)
            .Select(c => new DistributorClaim { ClaimId = c.Header.ClaimId, Period = c.Header.Period, Status = c.Header.Status }).ToList());

    public Task<IReadOnlyList<DistributorAgreement>> ListAgreementsAsync(int distributorId) =>
        Task.FromResult<IReadOnlyList<DistributorAgreement>>([new DistributorAgreement { AgreementNo = "TR-2026-001", TiersText = "R;0:0;2000:1.5" }]);
}

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeApiStore _store = new();

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:TradeRebates", "Server=unused");
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IDistributorApiStore>();
                s.AddSingleton<IDistributorApiStore>(_store);
            });
        });
    }

    private HttpClient Client(string? key)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (key is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static object Claim(int period, params object[] lines) => new { period, reference = "SO-77", lines };

    private static object Line(string gtin = "06414000010015", string invoice = "INV-1") =>
        new { invoiceNo = invoice, invoiceDate = "2026-09-02", customerNo = "C1", gtin, quantity = 10, netAmount = 90.0 };

    [Fact]
    public async Task Ping_is_anonymous()
    {
        var r = await Client(null).GetAsync("/ping");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nonsense")]
    [InlineData("tr_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Claims_need_a_valid_key(string? key)
    {
        var r = await Client(key).GetAsync("/api/v1/claims");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Submit_claim_creates_claim_for_the_key_owner()
    {
        var r = await Client(FakeApiStore.KespoKey).PostAsJsonAsync("/api/v1/claims", Claim(202609, Line(), Line(invoice: "INV-2")));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<SubmitClaimResponse>();
        Assert.Equal("To review", body!.Status);
        Assert.Equal(2, body.Lines);
        Assert.Equal(1, _store.Submitted.Single().DistributorId);
        Assert.Equal($"/api/v1/claims/{body.ClaimId}", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Submit_claim_validates_request()
    {
        var r = await Client(FakeApiStore.KespoKey).PostAsJsonAsync("/api/v1/claims", Claim(202613, Line(gtin: "ABC")));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        string text = await r.Content.ReadAsStringAsync();
        Assert.Contains("period", text);
        Assert.Contains("lines[0]", text);
        Assert.Empty(_store.Submitted);
    }

    [Fact]
    public async Task Database_rules_become_422()
    {
        var r = await Client(FakeApiStore.KespoKey).PostAsJsonAsync("/api/v1/claims", Claim(209901, Line()));
        Assert.Equal((HttpStatusCode)422, r.StatusCode);
        Assert.Contains("not active", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Distributors_only_see_their_own_claims()
    {
        var created = await Client(FakeApiStore.KespoKey).PostAsJsonAsync("/api/v1/claims", Claim(202609, Line()));
        string location = created.Headers.Location!.ToString();

        Assert.Equal(HttpStatusCode.OK, (await Client(FakeApiStore.KespoKey).GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(FakeApiStore.MetroKey).GetAsync(location)).StatusCode);

        var metroList = await Client(FakeApiStore.MetroKey).GetFromJsonAsync<List<DistributorClaim>>("/api/v1/claims");
        Assert.Empty(metroList!);
    }

    [Fact]
    public async Task Agreements_are_listed()
    {
        var list = await Client(FakeApiStore.MetroKey).GetFromJsonAsync<List<DistributorAgreement>>("/api/v1/agreements");
        Assert.Single(list!);
    }
}
