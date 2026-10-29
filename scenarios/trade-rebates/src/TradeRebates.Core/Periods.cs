using System.Globalization;
using System.Text.RegularExpressions;

namespace ContosoFoods.TradeRebates.Core;

public static partial class Periods
{
    public static int FromDate(DateTime date) => date.Year * 100 + date.Month;

    public static string Format(int period) => $"{period / 100}-{period % 100:00}";

    public static bool TryParse(string? text, out int period)
    {
        period = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string t = text.Trim().Replace("-", "");
        return int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out period) && t.Length == 6
            && Rules.ClaimFileParser.IsValidPeriod(period);
    }

    public static string QuarterOf(DateTime date) => $"{date.Year}Q{(date.Month - 1) / 3 + 1}";

    public static string PreviousQuarter(DateTime today) => QuarterOf(new DateTime(today.Year, today.Month, 1).AddMonths(-3));

    public static bool IsQuarter(string? text) => text is not null && QuarterPattern().IsMatch(text);

    public static IReadOnlyList<int> PeriodsOf(string quarter)
    {
        if (!IsQuarter(quarter))
            throw new FormatException("Quarter must be yyyyQn.");
        int year = int.Parse(quarter[..4], CultureInfo.InvariantCulture);
        int q = quarter[5] - '0';
        return Enumerable.Range((q - 1) * 3 + 1, 3).Select(m => year * 100 + m).ToList();
    }

    [GeneratedRegex(@"^20\d\dQ[1-4]$")]
    private static partial Regex QuarterPattern();
}
