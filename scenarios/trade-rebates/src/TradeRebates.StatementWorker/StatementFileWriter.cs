using System.Text;
using ContosoFoods.TradeRebates.Core;
using Microsoft.Extensions.Options;

namespace ContosoFoods.TradeRebates.StatementWorker;

public interface IStatementFileWriter
{
    /// <summary>Writes the HTML statement and CSV detail; returns the path of the HTML file.</summary>
    string Write(StatementDocument doc, DateTime printedAt);
}

public sealed class StatementFileWriter(IOptions<StatementOptions> options) : IStatementFileWriter
{
    public string Write(StatementDocument doc, DateTime printedAt)
    {
        string root = options.Value.Root;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Statements:Root is not configured.");

        string folder = Path.Combine(root, StatementRenderer.RelativeFolder(doc.Header));
        Directory.CreateDirectory(folder);
        string baseName = StatementRenderer.FileBaseName(doc.Header);
        string html = Path.Combine(folder, baseName + ".html");
        string csv = Path.Combine(folder, baseName + ".csv");

        WriteAtomic(html, StatementRenderer.RenderHtml(doc, printedAt), new UTF8Encoding(false));
        // The finance import reads the detail in Windows-1252.
        WriteAtomic(csv, StatementRenderer.RenderCsv(doc), Encoding.GetEncoding(1252));
        return html;
    }

    private static void WriteAtomic(string path, string content, Encoding encoding)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, content, encoding);
        File.Move(temp, path, overwrite: true);
    }
}
