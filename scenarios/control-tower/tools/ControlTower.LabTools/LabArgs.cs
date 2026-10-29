namespace Proseware.ControlTower.LabTools;

public sealed class LabArgumentException : Exception
{
    public LabArgumentException(string message) : base(message)
    {
    }
}

/// <summary>Minimal "--name value" / "--flag" option parser for the lab commands.</summary>
public sealed class LabArgs
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = new();

    public LabArgs(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var name = a[2..];
                var eq = name.IndexOf('=');
                if (eq > 0)
                    _options[name[..eq]] = name[(eq + 1)..];
                else if (i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal))
                    _options[name] = list[++i];
                else
                    _options[name] = null;
            }
            else
            {
                _positional.Add(a);
            }
        }
    }

    public IReadOnlyList<string> Positional => _positional;

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.TryGetValue(name, out var v) ? v : null;

    public string Get(string name, string defaultValue) => _options.TryGetValue(name, out var v) && v is not null ? v : defaultValue;

    public string Required(string name) =>
        _options.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v : throw new LabArgumentException($"Option --{name} is required.");

    public int GetInt(string name, int defaultValue) =>
        _options.TryGetValue(name, out var v) && v is not null
            ? int.TryParse(v, out var i) ? i : throw new LabArgumentException($"Option --{name} must be a number.")
            : defaultValue;
}
