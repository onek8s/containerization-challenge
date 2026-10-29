using System.Text.Json;

namespace Humongous.Documents.Wording;

public sealed record Clause(string Title, string Text);

/// <summary>Endorsement clause wording for the clause codes returned by RateCore.</summary>
public sealed class ClauseLibrary
{
    private readonly Dictionary<string, Clause> _clauses;

    public ClauseLibrary()
    {
        using var stream = typeof(ClauseLibrary).Assembly.GetManifestResourceStream("Humongous.Documents.Wording.clauses.json")
            ?? throw new InvalidOperationException("Clause wording resource is missing.");
        _clauses = JsonSerializer.Deserialize<Dictionary<string, Clause>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
    }

    public Clause Get(string code) =>
        _clauses.TryGetValue(code, out var clause) ? clause : new Clause(code, "Wording available from your underwriter.");
}
