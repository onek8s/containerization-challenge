using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Jobs.Cards;

public sealed record CardImportResult(int Imported, int Duplicates, IReadOnlyList<string> Rejected);

public sealed class CardTransactionImporter
{
    private readonly ExpenseClaimsDbContext _db;

    public CardTransactionImporter(ExpenseClaimsDbContext db) => _db = db;

    public static IReadOnlyList<CardTransactionRecord> Read(Stream csv, string culture)
    {
        var config = new CsvConfiguration(CultureInfo.GetCultureInfo(culture))
        {
            Delimiter = ";",
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null
        };
        // The card company still sends Windows-1252; ä/ö in merchant names otherwise break.
        using var reader = new StreamReader(csv, CodePagesEncoding.Windows1252, detectEncodingFromByteOrderMarks: true);
        using var parser = new CsvReader(reader, config);
        return parser.GetRecords<CardTransactionRecord>().ToList();
    }

    public async Task<CardImportResult> ImportAsync(IReadOnlyList<CardTransactionRecord> records, string sourceFile, CancellationToken ct)
    {
        var cards = await _db.CompanyCards.Where(c => c.Active).ToDictionaryAsync(c => c.MaskedNumber, ct);
        var references = records.Select(r => r.Reference).ToList();
        var known = (await _db.CardTransactions.Where(t => references.Contains(t.Reference)).Select(t => t.Reference).ToListAsync(ct)).ToHashSet();

        var rejected = new List<string>();
        int imported = 0, duplicates = 0;
        var now = DateTime.Now;
        foreach (var r in records)
        {
            if (known.Contains(r.Reference))
            {
                duplicates++;
                continue;
            }
            if (!cards.TryGetValue(r.MaskedNumber, out var card))
            {
                rejected.Add($"{r.Reference}: card {r.MaskedNumber} is not linked to an employee");
                continue;
            }
            if (!string.Equals(r.BilledCurrency, "EUR", StringComparison.OrdinalIgnoreCase))
            {
                rejected.Add($"{r.Reference}: billed in {r.BilledCurrency}");
                continue;
            }

            _db.CardTransactions.Add(new CardTransaction
            {
                Reference = r.Reference,
                CompanyCard = card,
                TransactionDate = DateOnly.FromDateTime(r.TransactionDate),
                PostingDate = DateOnly.FromDateTime(r.PostingDate),
                Merchant = r.Merchant,
                MerchantCity = r.MerchantCity,
                MerchantCountry = string.IsNullOrWhiteSpace(r.MerchantCountry) ? null : r.MerchantCountry.ToUpperInvariant(),
                MerchantCategoryCode = r.MerchantCategoryCode,
                OriginalAmount = r.OriginalAmount,
                OriginalCurrency = r.OriginalCurrency.ToUpperInvariant(),
                BilledAmountEur = r.BilledAmount,
                SourceFile = sourceFile,
                ImportedAt = now
            });
            known.Add(r.Reference);
            imported++;
        }

        await _db.SaveChangesAsync(ct);
        return new CardImportResult(imported, duplicates, rejected);
    }
}

internal static class CodePagesEncoding
{
    private static readonly Lazy<Encoding> Encoding1252 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    });

    public static Encoding Windows1252 => Encoding1252.Value;
}
