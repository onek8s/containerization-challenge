using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Integration;

public sealed class GeneralLedgerOptions
{
    public const string Section = "GeneralLedger";
    /// <summary>Folder picked up by the managed file transfer agent of the finance system.</summary>
    public string OutboundFolder { get; set; } = @"D:\Interfaces\GL\Outbound";
    public string CompanyCode { get; set; } = "1100";
    public string SourceSystem { get; set; } = "LUCLEND";
}

public sealed record JournalLine(DateOnly PostingDate, string GlAccount, string CostCenter, decimal Debit, decimal Credit, string Text, string Reference);

/// <summary>
/// Writes the daily journal file for the finance system (SAP FI generic journal interface, layout GLJ2,
/// ISO-8859-15, semicolon separated, decimal comma).
/// </summary>
public sealed class GeneralLedgerFileWriter
{
    private static readonly CultureInfo Fi = CultureInfo.GetCultureInfo("fi-FI");
    private readonly GeneralLedgerOptions _options;

    static GeneralLedgerFileWriter() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public GeneralLedgerFileWriter(IOptions<GeneralLedgerOptions> options) => _options = options.Value;

    public string Write(string batchId, DateOnly businessDate, IReadOnlyList<JournalLine> lines)
    {
        if (lines.Sum(l => l.Debit) != lines.Sum(l => l.Credit))
            throw new InvalidOperationException($"Journal {batchId} does not balance.");
        Directory.CreateDirectory(_options.OutboundFolder);
        var name = $"GLJ2_{_options.SourceSystem}_{businessDate:yyyyMMdd}_{batchId}.csv";
        var temp = Path.Combine(_options.OutboundFolder, name + ".tmp");
        var target = Path.Combine(_options.OutboundFolder, name);
        using (var writer = new StreamWriter(temp, false, Encoding.GetEncoding(28605)))
        {
            writer.NewLine = "\r\n";
            writer.WriteLine($"H;{_options.SourceSystem};{_options.CompanyCode};{batchId};{businessDate:yyyyMMdd};{DateTime.Now:yyyyMMddHHmmss};{lines.Count}");
            var n = 0;
            foreach (var l in lines)
            {
                n++;
                writer.WriteLine(string.Join(';', "L", n.ToString(CultureInfo.InvariantCulture), l.PostingDate.ToString("yyyyMMdd"), l.GlAccount,
                    l.CostCenter, l.Debit.ToString("0.00", Fi), l.Credit.ToString("0.00", Fi), Clean(l.Text, 50), Clean(l.Reference, 20), "EUR"));
            }
            writer.WriteLine($"T;{lines.Sum(l => l.Debit).ToString("0.00", Fi)};{lines.Sum(l => l.Credit).ToString("0.00", Fi)}");
        }
        File.Move(temp, target, overwrite: true);
        return target;
    }

    private static string Clean(string s, int max)
    {
        var t = s.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
        return t.Length > max ? t[..max] : t;
    }
}
