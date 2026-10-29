using System.Xml.Linq;
using ExpenseClaims.Core.Payments;

namespace ExpenseClaims.Tests.Core;

public class PaymentsTests
{
    [Theory]
    [InlineData("FI21 1234 5600 0007 85", true)]
    [InlineData("FI2112345600000785", true)]
    [InlineData("fi21 1234 5600 0007 85", true)]
    [InlineData("FI22 1234 5600 0007 85", false)]
    [InlineData("FI21 1234 5600 0007 8", false)]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Iban_validation(string? iban, bool valid) => Assert.Equal(valid, Iban.IsValid(iban));

    [Fact]
    public void Iban_is_formatted_in_groups_of_four() =>
        Assert.Equal("FI21 1234 5600 0007 85", Iban.Format("FI2112345600000785"));

    [Fact]
    public void Pain001_contains_control_sums_and_transactions()
    {
        var debtor = new DebtorAccount("Lamna Healthcare Oy", "FI75 1572 3500 0501 36", "OKOYFIHH", "2468135-7");
        var payments = new[]
        {
            new PaymentInstruction("E1", "Aino Laine", "FI1447380010009051", "POPFFI22", 227.40m, "Expense claims EC-2022-000001"),
            new PaymentInstruction("E2", "Jari Mäkinen", "FI3715903000000776", null, 1000.05m, "Expense claims EC-2022-000002 EC-2022-000003")
        };

        var xml = Pain001Writer.Write("LAMNA-EXP-20221014130000", new DateTime(2022, 10, 14, 13, 0, 0), new DateOnly(2022, 10, 17), debtor, payments);
        var doc = XDocument.Load(new MemoryStream(xml));
        XNamespace ns = Pain001Writer.Namespace;

        Assert.Equal("2", doc.Descendants(ns + "GrpHdr").Single().Element(ns + "NbOfTxs")!.Value);
        Assert.Equal("1227.45", doc.Descendants(ns + "GrpHdr").Single().Element(ns + "CtrlSum")!.Value);
        Assert.Equal("2022-10-17", doc.Descendants(ns + "ReqdExctnDt").Single().Value);
        Assert.Equal("FI7515723500050136", doc.Descendants(ns + "DbtrAcct").Single().Descendants(ns + "IBAN").Single().Value);
        var tx = doc.Descendants(ns + "CdtTrfTxInf").ToList();
        Assert.Equal(2, tx.Count);
        Assert.Equal("Jari Makinen", tx[1].Element(ns + "Cdtr")!.Element(ns + "Nm")!.Value);
        Assert.Null(tx[1].Element(ns + "CdtrAgt"));
    }

    [Theory]
    [InlineData("Jari Mäkinen", "Jari Makinen")]
    [InlineData("Åsa Öberg & Co", "Asa Oberg   Co")]
    [InlineData("EC-2022-000001", "EC-2022-000001")]
    public void Free_text_is_limited_to_sepa_characters(string input, string expected) =>
        Assert.Equal(expected, Pain001Writer.Text(input, 70));

    [Fact]
    public void Empty_payment_file_is_not_allowed() =>
        Assert.Throws<ArgumentException>(() => Pain001Writer.Write("X", DateTime.Now, DateOnly.FromDateTime(DateTime.Today),
            new DebtorAccount("A", "FI7515723500050136", "OKOYFIHH", "1"), Array.Empty<PaymentInstruction>()));
}
