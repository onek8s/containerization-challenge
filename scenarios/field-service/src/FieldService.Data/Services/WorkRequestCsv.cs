using System.Text;
using FieldService.Data.Entities;

namespace FieldService.Data.Services;

/// <summary>
/// Reads the job request files that key accounts drop on the file share.
/// Expected header: Account;Reference;Site;Summary;Description;Priority
/// </summary>
public static class WorkRequestCsv
{
    public static readonly string[] Header = { "Account", "Reference", "Site", "Summary", "Description", "Priority" };

    public static WorkRequestParseResult Parse(TextReader reader)
    {
        var result = new WorkRequestParseResult();
        var headerLine = reader.ReadLine();
        if (headerLine == null)
        {
            result.Errors.Add("File is empty.");
            return result;
        }

        var header = SplitLine(headerLine);
        if (!header.Select(h => h.Trim()).SequenceEqual(Header, StringComparer.OrdinalIgnoreCase))
        {
            result.Errors.Add("Unexpected header: " + headerLine);
            return result;
        }

        var lineNo = 1;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cols = SplitLine(line);
            if (cols.Count != Header.Length)
            {
                result.Errors.Add($"Line {lineNo}: expected {Header.Length} columns, got {cols.Count}.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(cols[0]) || string.IsNullOrWhiteSpace(cols[2]) || string.IsNullOrWhiteSpace(cols[3]))
            {
                result.Errors.Add($"Line {lineNo}: account, site and summary are required.");
                continue;
            }

            if (!TryParsePriority(cols[5], out var priority))
            {
                result.Errors.Add($"Line {lineNo}: unknown priority '{cols[5]}'.");
                continue;
            }

            result.Requests.Add(new NewWorkOrder(
                cols[0].Trim().ToUpperInvariant(),
                cols[2],
                cols[3],
                string.IsNullOrWhiteSpace(cols[4]) ? null : cols[4],
                priority,
                "Import",
                string.IsNullOrWhiteSpace(cols[1]) ? null : cols[1].Trim()));
        }

        return result;
    }

    private static bool TryParsePriority(string value, out WorkOrderPriority priority)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            priority = WorkOrderPriority.Normal;
            return true;
        }

        switch (value.ToUpperInvariant())
        {
            case "P1": priority = WorkOrderPriority.Emergency; return true;
            case "P2": priority = WorkOrderPriority.High; return true;
            case "P3": priority = WorkOrderPriority.Normal; return true;
            case "P4": priority = WorkOrderPriority.Low; return true;
        }

        return Enum.TryParse(value, true, out priority) && Enum.IsDefined(priority);
    }

    internal static List<string> SplitLine(string line)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ';')
            {
                cols.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        cols.Add(sb.ToString());
        return cols;
    }
}

public class WorkRequestParseResult
{
    public List<NewWorkOrder> Requests { get; } = new();

    public List<string> Errors { get; } = new();
}
