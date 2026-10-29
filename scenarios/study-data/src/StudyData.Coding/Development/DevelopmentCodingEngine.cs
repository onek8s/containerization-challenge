using System.IO;
using StudyData.Core.Coding;

namespace StudyData.Coding.Development;

/// <summary>Development stand-in for the autocoder: a small built-in term list with fictional codes.</summary>
public sealed class DevelopmentCodingEngine : ICodingEngine
{
    private record Entry(string Dictionary, CodingCandidate Candidate, string[] Keys);

    private readonly List<Entry> _entries;

    public DevelopmentCodingEngine()
    {
        using var stream = typeof(DevelopmentCodingEngine).Assembly.GetManifestResourceStream(
            typeof(DevelopmentCodingEngine).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("dictionary.txt", StringComparison.Ordinal)))!;
        using var reader = new StreamReader(stream);
        _entries = new List<Entry>();
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var p = line.Split('\t');
            var synonyms = p.Length > 6 ? p[6].Split('|', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
            _entries.Add(new Entry(p[0], new CodingCandidate(p[1], p[2], p[3], p[4], p[5], 0),
                synonyms.Prepend(p[2]).Select(CodingRules.Normalise).ToArray()));
        }
    }

    public string DictionaryVersion(string dictionary) => $"{dictionary.ToUpperInvariant()} development list";

    public IReadOnlyList<CodingCandidate> Search(string dictionary, string verbatim, int maxCandidates)
    {
        var term = CodingRules.Normalise(verbatim);
        var words = term.Split(' ');
        return _entries
            .Where(e => e.Dictionary.Equals(dictionary, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Candidate with { Score = Score(e.Keys, term, words) })
            .Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Term)
            .Take(maxCandidates)
            .ToList();
    }

    private static int Score(string[] keys, string term, string[] words)
    {
        // The term itself and its listed synonyms (lowest level terms / trade names) are exact matches.
        if (keys.Contains(term))
        {
            return 100;
        }

        var best = 0;
        foreach (var key in keys)
        {
            var keyWords = key.Split(' ');
            var common = words.Count(w => w.Length > 2 && keyWords.Contains(w));
            if (common > 0)
            {
                best = Math.Max(best, 40 + 50 * common / Math.Max(words.Length, keyWords.Length));
            }
        }

        return best;
    }
}
