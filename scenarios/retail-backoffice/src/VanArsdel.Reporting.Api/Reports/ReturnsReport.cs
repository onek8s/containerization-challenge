using System.Data.Common;
using Dapper;

namespace VanArsdel.Reporting.Api.Reports;

/// <summary>Return rates per category and grade mix over 30 days. Buyers use it to spot sizing problems.</summary>
public class ReturnsReport : IReport
{
    private const string Sql =
        "SELECT COALESCE(p.CategoryCode, '?') AS Category, " +
        "SUM(r.Quantity) AS Units, SUM(r.RefundAmountEur) AS RefundEur, " +
        "SUM(CASE WHEN r.Grade = 'A' THEN r.Quantity ELSE 0 END) AS GradeA, " +
        "SUM(CASE WHEN r.Grade = 'B' THEN r.Quantity ELSE 0 END) AS GradeB, " +
        "SUM(CASE WHEN r.Grade = 'C' THEN r.Quantity ELSE 0 END) AS GradeC, " +
        "SUM(CASE WHEN r.StoreCode IS NULL THEN r.Quantity ELSE 0 END) AS ByPost " +
        "FROM FactReturn r LEFT JOIN DimProduct p ON p.Sku = r.Sku " +
        "WHERE r.BusinessDate > @from AND r.BusinessDate <= @to GROUP BY COALESCE(p.CategoryCode, '?')";

    private const string SoldSql =
        "SELECT COALESCE(p.CategoryCode, '?') AS Category, SUM(f.Quantity) AS Units " +
        "FROM FactSales f LEFT JOIN DimProduct p ON p.Sku = f.Sku " +
        "WHERE f.Quantity > 0 AND f.BusinessDate > @from AND f.BusinessDate <= @to GROUP BY COALESCE(p.CategoryCode, '?')";

    public string Name => "returns";
    public string Title => "Returns by category (30 days)";

    public async Task<List<ReportTable>> BuildAsync(DbConnection connection, DateTime businessDate, CancellationToken cancellationToken)
    {
        var args = new { from = businessDate.Date.AddDays(-30), to = businessDate.Date };
        var returns = (await connection.QueryAsync<ReturnRow>(new CommandDefinition(Sql, args, cancellationToken: cancellationToken))).ToList();
        var sold = (await connection.QueryAsync<(string Category, int Units)>(new CommandDefinition(SoldSql, args, cancellationToken: cancellationToken)))
            .ToDictionary(s => s.Category, s => s.Units);

        var table = new ReportTable
        {
            Sheet = "Returns",
            Columns =
            {
                new ReportColumn("Category", width: 10), new ReportColumn("Sold", Formats.Integer, 9), new ReportColumn("Returned", Formats.Integer, 9),
                new ReportColumn("Return rate", Formats.Percent, 11), new ReportColumn("Refunds EUR", Formats.Money), new ReportColumn("Grade A", Formats.Percent, 9),
                new ReportColumn("Grade B", Formats.Percent, 9), new ReportColumn("Grade C", Formats.Percent, 9), new ReportColumn("By post", Formats.Percent, 9),
            },
        };
        foreach (var r in returns.OrderByDescending(r => r.Units))
        {
            var s = sold.TryGetValue(r.Category, out var u) ? u : 0;
            table.Rows.Add(new object[]
            {
                r.Category, s, r.Units, Formats.Ratio(r.Units, s), r.RefundEur,
                Formats.Ratio(r.GradeA, r.Units), Formats.Ratio(r.GradeB, r.Units), Formats.Ratio(r.GradeC, r.Units), Formats.Ratio(r.ByPost, r.Units),
            });
        }
        var totalReturned = returns.Sum(r => r.Units);
        var totalSold = sold.Values.Sum();
        table.Totals = new object[] { "Total", totalSold, totalReturned, Formats.Ratio(totalReturned, totalSold), returns.Sum(r => r.RefundEur), null, null, null, null };
        return new List<ReportTable> { table };
    }

    private class ReturnRow
    {
        public string Category { get; set; }
        public int Units { get; set; }
        public decimal RefundEur { get; set; }
        public int GradeA { get; set; }
        public int GradeB { get; set; }
        public int GradeC { get; set; }
        public int ByPost { get; set; }
    }
}
