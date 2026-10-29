using System.Runtime.CompilerServices;
using System.Text;
using Coho.Billing.Core;
using Coho.Billing.Core.Rating;

namespace Coho.Billing.Core.Tests;

internal static class TestData
{
    [ModuleInitializer]
    internal static void RegisterEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    public static BillingAccount Account(decimal averageDailyUse = 0.34m, int dials = 5, decimal multiplier = 1m, string tariff = "DOM") => new BillingAccount
    {
        AccountNumber = "10042117",
        CustomerName = "Anna Lindqvist",
        AddressLine1 = "Harbour Road 14",
        PostalCode = "20100",
        City = "Northport",
        TariffCode = tariff,
        CycleCode = "N1",
        PaymentMethod = PaymentMethod.DirectDebit,
        Iban = "FI2112345600000785",
        MandateId = "CW-10042117",
        MandateSignedOn = new DateTime(2019, 6, 1),
        AverageDailyUse = averageDailyUse,
        Meter = new Meter { Serial = "W19A004417", Dials = dials, Multiplier = multiplier, SizeCode = "DN20" }
    };

    public static MeterRead Read(DateTime date, decimal reading, string code = "00") => new MeterRead
    {
        MeterSerial = "W19A004417",
        ReadDate = date,
        Reading = reading,
        ReadType = ReadType.Actual,
        Source = ReadSource.Handheld,
        ReaderCode = code
    };

    public static List<Tariff> Tariffs()
    {
        var old = new Tariff { Code = "DOM", Name = "Domestic", ValidFrom = new DateTime(2025, 1, 1), StandingChargePerDay = 0.2137m, WastewaterPrice = 2.41m, VatRate = 25.5m };
        old.WaterBlocks.Add(new TariffBlock { UpTo = 10, Price = 1.38m });
        old.WaterBlocks.Add(new TariffBlock { UpTo = 25, Price = 1.71m });
        old.WaterBlocks.Add(new TariffBlock { UpTo = null, Price = 2.05m });

        var current = new Tariff { Code = "DOM", Name = "Domestic", ValidFrom = new DateTime(2026, 3, 1), StandingChargePerDay = 0.228m, WastewaterPrice = 2.56m, VatRate = 25.5m };
        current.WaterBlocks.Add(new TariffBlock { UpTo = 10, Price = 1.46m });
        current.WaterBlocks.Add(new TariffBlock { UpTo = 25, Price = 1.83m });
        current.WaterBlocks.Add(new TariffBlock { UpTo = null, Price = 2.19m });

        var noSewer = new Tariff { Code = "DOMNS", Name = "Domestic, no sewer", ValidFrom = new DateTime(2025, 1, 1), StandingChargePerDay = 0.2137m, WastewaterPrice = 0, WastewaterShare = 0, VatRate = 25.5m };
        noSewer.WaterBlocks.Add(new TariffBlock { UpTo = 10, Price = 1.38m });
        noSewer.WaterBlocks.Add(new TariffBlock { UpTo = null, Price = 1.71m });

        return new List<Tariff> { current, old, noSewer };
    }
}
