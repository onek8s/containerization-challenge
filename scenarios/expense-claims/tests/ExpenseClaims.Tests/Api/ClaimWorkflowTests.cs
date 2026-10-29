using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseClaims.Api.Models;
using ExpenseClaims.Api.Services;
using ExpenseClaims.Core.Claims;
using ExpenseClaims.Data.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseClaims.Tests.Api;

public sealed class ClaimWorkflowTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(), new DateOnlyJsonConverter() }
    };

    private readonly ApiFactory _factory;

    public ClaimWorkflowTests(ApiFactory factory) => _factory = factory;

    private static DateOnly DaysAgo(int days) => DateOnly.FromDateTime(DateTime.Today.AddDays(-days));

    private static ClaimLineRequest Line(ExpenseCategory category, decimal amount, string currency = "EUR", decimal? km = null,
        PerDiemType perDiem = PerDiemType.None, string? country = null, int daysAgo = 3) =>
        new(DaysAgo(daysAgo), category, $"{category} line", amount, currency, km, km is null ? null : "Espoo - Helsinki-Vantaa", perDiem, country, 0);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<ClaimDto> CreateClaimAsync(HttpClient client, string purpose)
    {
        var response = await client.PostAsJsonAsync("/api/claims", new CreateClaimRequest(purpose, null, "PR20114"), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<ClaimDto>(response);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/claims")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Claim_goes_from_draft_to_checked()
    {
        var aino = await _factory.ClientForAsync("aino.laine");
        var claim = await CreateClaimAsync(aino, "Wound care course, Stockholm");
        Assert.Equal(ClaimStatus.Draft, claim.Status);
        Assert.StartsWith($"EC-{DateTime.Today.Year}-", claim.Number);

        claim = await Read<ClaimDto>(await aino.PostAsJsonAsync($"/api/claims/{claim.Id}/lines", Line(ExpenseCategory.Accommodation, 1650m, "SEK"), Json));
        claim = await Read<ClaimDto>(await aino.PostAsJsonAsync($"/api/claims/{claim.Id}/lines", Line(ExpenseCategory.PerDiem, 0, perDiem: PerDiemType.Abroad, country: "SE"), Json));
        claim = await Read<ClaimDto>(await aino.PostAsJsonAsync($"/api/claims/{claim.Id}/lines", Line(ExpenseCategory.Mileage, 0, km: 42m, daysAgo: 4), Json));

        var hotel = claim.Lines.Single(l => l.Category == ExpenseCategory.Accommodation);
        Assert.Equal(144.08m, hotel.AmountEur);
        Assert.Equal(0m, hotel.VatRate);
        Assert.Equal(64m, claim.Lines.Single(l => l.Category == ExpenseCategory.PerDiem).AmountEur);
        Assert.Equal(19.32m, claim.Lines.Single(l => l.Category == ExpenseCategory.Mileage).AmountEur);
        Assert.Equal(227.40m, claim.Total);
        Assert.Contains(claim.Findings, f => f.Code == "RECEIPT_MISSING" && f.LineNumber == hotel.LineNumber);

        var refused = await aino.PostAsync($"/api/claims/{claim.Id}/submit", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        using var upload = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 receipt"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(pdf, "file", "scandic.pdf");
        claim = await Read<ClaimDto>(await aino.PostAsync($"/api/claims/{claim.Id}/lines/{hotel.Id}/receipt", upload));
        Assert.Equal("scandic.pdf", claim.Lines.Single(l => l.Id == hotel.Id).Receipt!.FileName);

        claim = await Read<ClaimDto>(await aino.PostAsync($"/api/claims/{claim.Id}/submit", null));
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("Jari Mäkinen", Assert.Single(claim.Approvals).Approver);
        Assert.False(claim.CanEdit);

        var oskari = await _factory.ClientForAsync("oskari.heikkinen");
        Assert.Equal(HttpStatusCode.NotFound, (await oskari.GetAsync($"/api/claims/{claim.Id}")).StatusCode);

        var jari = await _factory.ClientForAsync("jari.makinen");
        var pending = await Read<List<ClaimSummaryDto>>(await jari.GetAsync("/api/approvals"));
        Assert.Contains(pending, p => p.Id == claim.Id);
        var receiptId = claim.Lines.Single(l => l.Id == hotel.Id).Receipt!.Id;
        Assert.Equal("%PDF-1.4 receipt", await jari.GetStringAsync($"/api/receipts/{receiptId}"));

        claim = await Read<ClaimDto>(await jari.PostAsJsonAsync($"/api/approvals/{claim.Id}/approve", new DecisionRequest(null), Json));
        Assert.Equal(ClaimStatus.Approved, claim.Status);

        Assert.Equal(HttpStatusCode.Forbidden, (await jari.PostAsJsonAsync($"/api/finance/claims/{claim.Id}/check", new DecisionRequest(null), Json)).StatusCode);

        var mikko = await _factory.ClientForAsync("mikko.salonen");
        claim = await Read<ClaimDto>(await mikko.PostAsJsonAsync($"/api/finance/claims/{claim.Id}/check", new DecisionRequest("OK"), Json));
        Assert.Equal(ClaimStatus.Checked, claim.Status);
        Assert.Equal(new[] { "Created", "Submitted", "Approved", "Checked" }, claim.Events.Select(e => e.Action));
    }

    [Fact]
    public async Task Returned_claim_can_be_changed_by_the_claimant()
    {
        var aino = await _factory.ClientForAsync("aino.laine");
        var claim = await CreateClaimAsync(aino, "Equipment for home office");
        claim = await Read<ClaimDto>(await aino.PostAsJsonAsync($"/api/claims/{claim.Id}/lines", Line(ExpenseCategory.Training, 24m), Json));
        claim = await Read<ClaimDto>(await aino.PostAsJsonAsync($"/api/claims/{claim.Id}/lines", Line(ExpenseCategory.Other, 18m), Json));
        Assert.Equal(4.65m, claim.Lines[0].VatAmountEur);

        claim = await Read<ClaimDto>(await aino.PutAsJsonAsync($"/api/claims/{claim.Id}/lines/{claim.Lines[0].Id}", Line(ExpenseCategory.Training, 24.5m), Json));
        claim = await Read<ClaimDto>(await aino.PostAsync($"/api/claims/{claim.Id}/submit", null));

        var jari = await _factory.ClientForAsync("jari.makinen");
        var missingComment = await jari.PostAsJsonAsync($"/api/approvals/{claim.Id}/return", new DecisionRequest(""), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingComment.StatusCode);
        claim = await Read<ClaimDto>(await jari.PostAsJsonAsync($"/api/approvals/{claim.Id}/return", new DecisionRequest("Add the course name"), Json));
        Assert.Equal(ClaimStatus.Returned, claim.Status);
        Assert.False(claim.CanEdit);

        var mine = await Read<ClaimDto>(await aino.GetAsync($"/api/claims/{claim.Id}"));
        Assert.True(mine.CanEdit);
    }

    [Fact]
    public async Task Card_transaction_becomes_a_company_paid_line()
    {
        var factoryDb = _factory.Services.CreateScope();
        var db = factoryDb.ServiceProvider.GetRequiredService<ExpenseClaims.Data.ExpenseClaimsDbContext>();
        var card = db.CompanyCards.Single(c => c.MaskedNumber == "552235******4410");
        var transaction = new CardTransaction
        {
            Reference = Guid.NewGuid().ToString("N")[..20],
            CompanyCardId = card.Id,
            TransactionDate = DaysAgo(2),
            PostingDate = DaysAgo(1),
            Merchant = "SOKOS HOTEL TAMPERE",
            MerchantCountry = "FI",
            MerchantCategoryCode = "7011",
            OriginalAmount = 132m,
            OriginalCurrency = "EUR",
            BilledAmountEur = 132m,
            SourceFile = "test",
            ImportedAt = DateTime.Now
        };
        db.CardTransactions.Add(transaction);
        db.SaveChanges();

        var aino = await _factory.ClientForAsync("aino.laine");
        var open = await Read<List<CardTransactionDto>>(await aino.GetAsync("/api/card-transactions"));
        Assert.Contains(open, t => t.Id == transaction.Id && t.SuggestedCategory == ExpenseCategory.Accommodation);

        var claim = await CreateClaimAsync(aino, "Tampere seminar");
        claim = await Read<ClaimDto>(await aino.PostAsync($"/api/claims/{claim.Id}/lines/from-card/{transaction.Id}", null));
        claim = await Read<ClaimDto>(await aino.PostAsJsonAsync($"/api/claims/{claim.Id}/lines", Line(ExpenseCategory.PerDiem, 0, perDiem: PerDiemType.Full, daysAgo: 2), Json));

        var cardLine = claim.Lines.Single(l => l.PaidByCompany);
        Assert.Equal(12m, cardLine.VatAmountEur);
        Assert.Equal(180m, claim.Total);
        Assert.Equal(48m, claim.Reimbursable);

        var duplicate = await aino.PostAsync($"/api/claims/{claim.Id}/lines/from-card/{transaction.Id}", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var jari = await _factory.ClientForAsync("jari.makinen");
        Assert.Equal(HttpStatusCode.Forbidden, (await jari.PostAsync($"/api/claims/{claim.Id}/lines/from-card/{transaction.Id}", null)).StatusCode);
    }

    [Fact]
    public async Task Me_reports_roles_and_approver()
    {
        var jari = await _factory.ClientForAsync("jari.makinen");
        var me = await Read<MeDto>(await jari.GetAsync("/api/me"));
        Assert.True(me.IsApprover);
        Assert.False(me.IsFinance);

        var laura = await _factory.ClientForAsync("laura.niemi");
        me = await Read<MeDto>(await laura.GetAsync("/api/me"));
        Assert.True(me.IsAdministrator);
        Assert.True(me.IsFinance);
        Assert.Equal(HttpStatusCode.OK, (await laura.GetAsync("/api/admin/cards")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jari.GetAsync("/api/admin/cards")).StatusCode);
    }

    [Fact]
    public async Task Unknown_user_gets_a_token_refused() =>
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _factory.CreateClient().PostAsJsonAsync("/dev/token", new { upn = "someone@lamnahealthcare.com" })).StatusCode);
}
