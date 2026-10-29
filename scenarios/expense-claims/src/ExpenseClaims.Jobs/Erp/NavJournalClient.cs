using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseClaims.Core.Accounting;
using Microsoft.Extensions.Options;

namespace ExpenseClaims.Jobs.Erp;

/// <summary>Writes general journal lines to Dynamics NAV through its OData V4 web service.</summary>
public sealed class NavJournalClient
{
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _http;
    private readonly ErpOptions _options;

    public NavJournalClient(HttpClient http, IOptions<ErpOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    private string EntitySet =>
        $"Company('{Uri.EscapeDataString(_options.Company.Replace("'", "''"))}')/{_options.JournalService}";

    /// <summary>Highest line number in the journal batch, so new lines are appended after lines finance has not posted yet.</summary>
    public async Task<int> GetLastLineNoAsync(CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"Journal_Template_Name eq '{_options.JournalTemplate}' and Journal_Batch_Name eq '{_options.JournalBatch}'");
        var url = $"{EntitySet}?$filter={filter}&$orderby=Line_No desc&$top=1&$select=Line_No";
        var result = await _http.GetFromJsonAsync<ODataList>(url, ct);
        return result?.Value.FirstOrDefault()?.Line_No ?? 0;
    }

    public async Task<bool> DocumentExistsAsync(string documentNo, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"Journal_Batch_Name eq '{_options.JournalBatch}' and Document_No eq '{documentNo}'");
        var result = await _http.GetFromJsonAsync<ODataList>($"{EntitySet}?$filter={filter}&$top=1&$select=Line_No", ct);
        return result?.Value.Count > 0;
    }

    public async Task InsertAsync(JournalLine line, int lineNo, CancellationToken ct)
    {
        var body = new NavJournalLine
        {
            Journal_Template_Name = _options.JournalTemplate,
            Journal_Batch_Name = _options.JournalBatch,
            Line_No = lineNo,
            Posting_Date = line.PostingDate.ToString("yyyy-MM-dd"),
            Document_No = line.DocumentNo,
            External_Document_No = line.DocumentNo,
            Account_Type = "G/L Account",
            Account_No = line.AccountNo,
            Description = line.Description,
            Amount = line.Amount,
            Shortcut_Dimension_1_Code = line.CostCenter,
            Shortcut_Dimension_2_Code = line.Project,
            Employee_No = line.EmployeeNumber
        };
        using var response = await _http.PostAsJsonAsync(EntitySet, body, Json, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"NAV rejected journal line {line.DocumentNo}/{lineNo}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
    }

    private sealed class ODataList
    {
        public List<NavJournalLine> Value { get; set; } = new();
    }

    // Field names as NAV publishes them for the journal page.
    private sealed class NavJournalLine
    {
        public string? Journal_Template_Name { get; set; }
        public string? Journal_Batch_Name { get; set; }
        public int Line_No { get; set; }
        public string? Posting_Date { get; set; }
        public string? Document_No { get; set; }
        public string? External_Document_No { get; set; }
        public string? Account_Type { get; set; }
        public string? Account_No { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Shortcut_Dimension_1_Code { get; set; }
        public string? Shortcut_Dimension_2_Code { get; set; }
        public string? Employee_No { get; set; }
    }
}
