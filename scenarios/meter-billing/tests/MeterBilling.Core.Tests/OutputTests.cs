using System.Text;
using System.Xml.Linq;
using Coho.Billing.Core.Output;
using Coho.Billing.Core.Rating;
using Coho.Billing.Core.Runs;
using Xunit;

namespace Coho.Billing.Core.Tests;

public class OutputTests
{
    private static readonly XNamespace Pain = SepaDirectDebitWriter.Namespace;

    [Fact]
    public void Creditor_reference_matches_iso_example()
    {
        Assert.Equal("RF18539007547034", CreditorReference.Create("539007547034"));
        Assert.True(CreditorReference.IsValid("RF18 5390 0754 7034"));
        Assert.False(CreditorReference.IsValid("RF19539007547034"));
        Assert.Equal("RF18 5390 0754 7034", CreditorReference.Format("RF18539007547034"));
    }

    [Fact]
    public void Payment_reference_is_valid()
    {
        var number = BillNumbers.Create(new DateTime(2026, 5, 3), 482, 17);
        Assert.Equal("260048200017", number);

        var reference = BillNumbers.PaymentReference("10042117", number);
        Assert.StartsWith("RF", reference);
        Assert.EndsWith("100421178200017", reference);
        Assert.True(CreditorReference.IsValid(reference));
    }

    [Theory]
    [InlineData("FI21 1234 5600 0007 85", true)]
    [InlineData("FI2112345600000786", false)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("FI21", false)]
    [InlineData(null, false)]
    public void Iban_check(string iban, bool valid)
    {
        Assert.Equal(valid, Iban.IsValid(iban));
    }

    [Fact]
    public void Direct_debit_file_groups_first_and_recurring_collections()
    {
        var batch = new DirectDebitBatch { MessageId = "CWDD20260503000482", CreatedAt = new DateTime(2026, 5, 3, 6, 0, 0), CollectionDate = new DateTime(2026, 5, 17) };
        batch.Items.Add(Item("260048200001", 177.11m, "Anna Lindqvist", "FI2112345600000785", false));
        batch.Items.Add(Item("260048200003", 52.40m, "Jonas Bergström", "FI5542345670000081", true));
        batch.Items.Add(Item("260048200005", 210.05m, "Leena Koski", "FI4250001510000023", false));

        var xml = Write(batch);

        var header = xml.Root.Element(Pain + "CstmrDrctDbtInitn").Element(Pain + "GrpHdr");
        Assert.Equal("3", header.Element(Pain + "NbOfTxs").Value);
        Assert.Equal("439.56", header.Element(Pain + "CtrlSum").Value);

        var groups = xml.Descendants(Pain + "PmtInf").ToList();
        Assert.Equal(new[] { "FRST", "RCUR" }, groups.Select(g => g.Descendants(Pain + "SeqTp").Single().Value));
        Assert.Equal("1", groups[0].Element(Pain + "NbOfTxs").Value);
        Assert.Equal("2026-05-17", groups[1].Element(Pain + "ReqdColltnDt").Value);
        Assert.Equal("Jonas Bergstrom", groups[0].Descendants(Pain + "Dbtr").Single().Element(Pain + "Nm").Value);
        Assert.Equal("FI22ZZZ12345678", groups[1].Descendants(Pain + "CdtrSchmeId").Descendants(Pain + "Id").Last().Value);

        var amount = groups[1].Descendants(Pain + "InstdAmt").First();
        Assert.Equal("EUR", amount.Attribute("Ccy").Value);
        Assert.Equal("177.11", amount.Value);
    }

    [Fact]
    public void Direct_debit_with_invalid_debtor_iban_is_refused()
    {
        var batch = new DirectDebitBatch { MessageId = "X", CreatedAt = DateTime.Now, CollectionDate = DateTime.Today };
        batch.Items.Add(Item("260048200001", 10m, "A", "FI2112345600000786", false));

        Assert.Throws<InvalidOperationException>(() => Write(batch));
    }

    [Fact]
    public void Print_file_has_fixed_width_records_in_code_page_1252()
    {
        var account = TestData.Account();
        account.CustomerName = "Eeva Mäkelä";
        account.AddressLine2 = "c/o Pekka Mäkelä";

        var bill = new BillCalculator().Calculate(account,
            new BillingPeriod { From = new DateTime(2026, 1, 28), To = new DateTime(2026, 4, 28), OpeningReading = 1832.4m, ClosingReading = 1863.1m, Consumption = 30.7m },
            TestData.Tariffs(), new DateTime(2026, 5, 3), 14);
        bill.BillNumber = "260048200001";
        bill.PaymentReference = BillNumbers.PaymentReference(account.AccountNumber, bill.BillNumber);

        using var stream = new MemoryStream();
        new PrintFileWriter().Write("RUN000482", new DateTime(2026, 5, 3, 6, 0, 0), new[] { new PrintItem { Bill = bill, Account = account } }, stream);

        var bytes = stream.ToArray();
        Assert.Contains((byte)0xE4, bytes);

        var lines = Encoding.GetEncoding(1252).GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("00CWPRT3RUN000482", lines[0]);
        Assert.Equal(99, lines[1].Length);
        Assert.StartsWith("10260048200001", lines[1]);
        Assert.Contains("2026012820260427A", lines[1]);
        Assert.Equal(162, lines[2].Length);
        Assert.StartsWith("11Eeva Mäkelä", lines[2]);

        var billLines = lines.Where(l => l.StartsWith("20")).ToList();
        Assert.Equal(bill.Lines.Count, billLines.Count);
        Assert.All(billLines, l => Assert.Equal(102, l.Length));

        var payment = lines.Single(l => l.StartsWith("30"));
        Assert.Equal("D", payment.Substring(38, 1));
        Assert.Contains("FI21**********0785", payment);

        var trailer = lines.Last();
        Assert.Equal("990000001" + (lines.Length).ToString("D9") + "+" + PrintFileWriter_Number(bill.TotalAmount), trailer);
    }

    private static string PrintFileWriter_Number(decimal total) => ((long)(total * 100)).ToString("D14");

    private static DirectDebitItem Item(string billNumber, decimal amount, string name, string iban, bool first) => new DirectDebitItem
    {
        EndToEndId = billNumber,
        Amount = amount,
        MandateId = "CW-" + billNumber,
        MandateSignedOn = new DateTime(2025, 1, 1),
        FirstCollection = first,
        DebtorName = name,
        DebtorIban = iban,
        CreditorReference = CreditorReference.Create(billNumber)
    };

    private static XDocument Write(DirectDebitBatch batch)
    {
        var writer = new SepaDirectDebitWriter(new CreditorSettings
        {
            Name = "Coho Water Ltd",
            Iban = "FI7944052020036082",
            Bic = "NDEAFIHH",
            CreditorId = "FI22ZZZ12345678"
        });

        using var stream = new MemoryStream();
        writer.Write(batch, stream);
        stream.Position = 0;
        return XDocument.Load(stream);
    }
}
