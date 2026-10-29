using ExpenseClaims.Data.Entities;
using ExpenseClaims.Jobs.Cards;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Tests.Jobs;

public class CardTransactionImporterTests
{
    private static IReadOnlyList<CardTransactionRecord> SampleFile()
    {
        using var file = File.OpenRead(TestFiles.Sample("cards", "TRX_20221012.csv"));
        return CardTransactionImporter.Read(file, "fi-FI");
    }

    [Fact]
    public void Sample_file_is_read_with_finnish_number_format()
    {
        var records = SampleFile();
        Assert.Equal(7, records.Count);
        var hotel = records.Single(r => r.Reference == "22284551920032");
        Assert.Equal(1890.00m, hotel.OriginalAmount);
        Assert.Equal("SEK", hotel.OriginalCurrency);
        Assert.Equal(174.22m, hotel.BilledAmount);
        Assert.Equal(new DateTime(2022, 10, 10), hotel.TransactionDate);
        Assert.Equal("CAFÉ SÖDERMALM", records.Single(r => r.Reference == "22284551920034").Merchant);
        Assert.Equal(-12.50m, records.Single(r => r.Reference == "22284551920036").BilledAmount);
    }

    [Fact]
    public async Task Transactions_are_matched_to_linked_cards_once()
    {
        using var database = new TestDatabase();
        await using (var db = database.CreateContext())
        {
            var aino = TestDatabase.Employee("100233", "Aino Laine");
            var jari = TestDatabase.Employee("100150", "Jari Makinen");
            db.CompanyCards.AddRange(
                new CompanyCard { MaskedNumber = "552235******4410", Employee = aino },
                new CompanyCard { MaskedNumber = "552235******7021", Employee = jari });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var result = await new CardTransactionImporter(db).ImportAsync(SampleFile(), "TRX_20221012.csv", CancellationToken.None);
            Assert.Equal(6, result.Imported);
            Assert.Contains("552235******9934", Assert.Single(result.Rejected));
        }

        await using (var db = database.CreateContext())
        {
            var again = await new CardTransactionImporter(db).ImportAsync(SampleFile(), "TRX_20221012.csv", CancellationToken.None);
            Assert.Equal(0, again.Imported);
            Assert.Equal(6, again.Duplicates);
            Assert.Equal(4, await db.CardTransactions.CountAsync(t => t.CompanyCard.Employee.EmployeeNumber == "100233"));
        }
    }
}
