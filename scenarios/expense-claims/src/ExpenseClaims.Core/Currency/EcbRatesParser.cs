using System.Globalization;
using System.Xml.Linq;

namespace ExpenseClaims.Core.Currency;

/// <summary>Parses the ECB euro foreign exchange reference rates feed (eurofxref-daily.xml / eurofxref-hist-90d.xml).</summary>
public static class EcbRatesParser
{
    private static readonly XNamespace Ecb = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";

    public static IReadOnlyList<ReferenceRate> Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var rates = new List<ReferenceRate>();
        foreach (var day in doc.Descendants(Ecb + "Cube").Where(e => e.Attribute("time") is not null))
        {
            var date = DateOnly.ParseExact(day.Attribute("time")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var cube in day.Elements(Ecb + "Cube"))
            {
                var currency = cube.Attribute("currency")?.Value;
                var rate = cube.Attribute("rate")?.Value;
                if (currency is null || rate is null)
                    continue;
                rates.Add(new ReferenceRate(date, currency, decimal.Parse(rate, NumberStyles.Number, CultureInfo.InvariantCulture)));
            }
        }
        return rates;
    }
}
