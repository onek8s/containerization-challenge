using System.Data.Common;

namespace VanArsdel.Reporting.Api.Reports;

public class ReportColumn
{
    public ReportColumn(string title, string format = null, double width = 14)
    {
        Title = title;
        Format = format;
        Width = width;
    }

    public string Title { get; }
    public string Format { get; }
    public double Width { get; }
}

public class ReportTable
{
    public string Sheet { get; set; }
    public List<ReportColumn> Columns { get; set; } = new();
    public List<object[]> Rows { get; set; } = new();
    public object[] Totals { get; set; }
}

public interface IReport
{
    string Name { get; }
    string Title { get; }
    Task<List<ReportTable>> BuildAsync(DbConnection connection, DateTime businessDate, CancellationToken cancellationToken);
}

public static class Formats
{
    public const string Money = "#,##0.00";
    public const string Integer = "#,##0";
    public const string Percent = "0.0%";
    public const string Date = "yyyy-mm-dd";

    public static decimal Ratio(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part / whole, 4);
}
