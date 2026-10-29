using PolicyChanges.Core.Flows;

namespace PolicyChanges.Tests.Fakes;

/// <summary>In-memory stand-in for the PolicyDesk screens used by the flows.</summary>
public sealed class FakeDesk : IDesktopSession
{
    private readonly List<FakeWindow> _windows = new();
    private FakeDialog? _dialog;
    private int _nextEndorsement = 6004101;

    public FakeDesk()
    {
        var main = Open("PolicyDesk 6.4 - DESK01 [TEST]");
        main.Field("Policy number");
        main.Button("Open", () => OpenPolicy(main.Fields["Policy number"]));
    }

    public Dictionary<string, FakePolicy> Policies { get; } = new();
    public List<string> Actions { get; } = new();
    public IReadOnlyList<FakeWindow> Windows => _windows;

    public IDesktopWindow WaitForWindow(string titlePattern, TimeSpan timeout)
    {
        if (_dialog is not null) throw new DesktopException($"PolicyDesk shows '{_dialog.Text}' instead of '{titlePattern}'.");
        return _windows.LastOrDefault(w => Wildcard.IsMatch(w.Title, titlePattern))
               ?? throw new DesktopException($"Window '{titlePattern}' did not appear.");
    }

    public IDesktopDialog? WaitForDialog(TimeSpan timeout) => _dialog;

    private FakeWindow Open(string title)
    {
        var w = new FakeWindow(this, title);
        _windows.Add(w);
        return w;
    }

    internal void Close(FakeWindow w) => _windows.Remove(w);

    internal void Show(string text, params (string Button, Action? Then)[] buttons)
    {
        if (buttons.Length == 0) buttons = new (string, Action?)[] { ("OK", null) };
        _dialog = new FakeDialog(text, buttons.Select(b => b.Button).ToList(), button =>
        {
            _dialog = null;
            buttons.First(b => b.Button == button).Then?.Invoke();
        });
    }

    private string Endorse(FakePolicy p, string type)
    {
        var number = (_nextEndorsement++).ToString();
        p.Endorsements.Add($"{number} {type}");
        return number;
    }

    private void OpenPolicy(string number)
    {
        if (!Policies.TryGetValue(number, out var p))
        {
            Show($"Policy {number} was not found.");
            return;
        }
        var w = Open($"PolicyDesk - Policy {number}");
        void Refresh()
        {
            w.Fields["Status"] = p.Status;
            w.Fields["Address"] = p.Address;
        }
        Refresh();
        w.Button("Close", () => Close(w));

        FakeWindow? Change(string title)
        {
            if (p.Status != "In force")
            {
                Show($"Policy {number} is not in force ({p.Status}). Changes are not allowed.");
                return null;
            }
            var c = Open($"PolicyDesk - {title} - {number}");
            c.Field("Effective date");
            c.Button("Cancel", () => Close(c));
            return c;
        }

        void Saved(FakeWindow c, string type, Action apply)
        {
            apply();
            var e = Endorse(p, type);
            Show($"Endorsement {e} created.", ("OK", () => { Close(c); Refresh(); }));
        }

        w.Button("Change address...", () =>
        {
            if (Change("Change address") is not { } c) return;
            c.Field("Street"); c.Field("Postal code"); c.Field("City");
            c.List("Country", "FI", "SE", "NO", "EE");
            c.Button("Save", () =>
            {
                if (c.Fields["Postal code"].Length != 5 && c.Fields["Country"] == "FI")
                {
                    Show($"Postal code {c.Fields["Postal code"]} is not valid for FI.");
                    return;
                }
                Saved(c, "Address", () => p.Address = $"{c.Fields["Street"]}, {c.Fields["Postal code"]} {c.Fields["City"]}, {c.Fields["Country"]}");
            });
        });

        w.Button("Change payment...", () =>
        {
            if (Change("Change payment") is not { } c) return;
            c.List("Payment method", "Direct debit", "Invoice");
            c.Field("IBAN"); c.Field("Account holder"); c.Field("Billing day");
            c.Button("Save", () => Saved(c, "Payment", () => p.Payment = $"{c.Fields["Payment method"]} {c.Fields["IBAN"]}".Trim()));
        });

        w.Button("Beneficiaries...", () =>
        {
            if (!p.Life)
            {
                Show("Beneficiaries can only be named on life policies.");
                return;
            }
            if (Change("Beneficiaries") is not { } c) return;
            var rows = p.Beneficiaries.ToList();
            c.Grid("Beneficiaries", () => rows);
            c.Field("Name"); c.Field("Date of birth"); c.Field("Share %");
            c.Button("Remove all", () =>
            {
                if (rows.Count > 0) Show("Remove all beneficiaries?", ("Yes", () => rows.Clear()), ("No", null));
            });
            c.Button("Add", () => rows.Add(new[] { c.Fields["Name"], c.Fields["Date of birth"], c.Fields["Share %"] }));
            c.Button("Save", () =>
            {
                var total = rows.Sum(r => int.Parse(r[2]));
                if (total != 100)
                {
                    Show($"Beneficiary shares must total 100 % (now {total} %).");
                    return;
                }
                Saved(c, "Beneficiaries", () => p.Beneficiaries = rows.ToList());
            });
        });

        w.Button("Cancel policy...", () =>
        {
            if (Change("Cancel policy") is not { } c) return;
            c.List("Reason", "Customer request", "Vehicle sold", "Death of policyholder", "Duplicate policy");
            c.Field("Refund IBAN");
            c.Field("Refund");
            c.Button("Calculate", () => c.Fields["Refund"] = "123,45 EUR");
            c.Button("Save", () => Show($"Cancel policy {number} from {c.Fields["Effective date"]}? This cannot be undone.",
                ("Yes", () =>
                {
                    p.Status = "Cancelled";
                    var e = Endorse(p, "Cancellation");
                    Show($"Endorsement {e} created. Refund 123,45 EUR by cheque.", ("OK", () => { Close(c); Refresh(); }));
                }),
                ("No", null)));
        });
    }
}

public sealed class FakePolicy
{
    public string Status { get; set; } = "In force";
    public string Address { get; set; } = "";
    public string Payment { get; set; } = "";
    public bool Life { get; set; }
    public List<string[]> Beneficiaries { get; set; } = new();
    public List<string> Endorsements { get; } = new();
}

public sealed class FakeWindow : IDesktopWindow
{
    private readonly FakeDesk _desk;
    private readonly Dictionary<string, Action> _buttons = new();
    private readonly Dictionary<string, string[]> _lists = new();
    private readonly Dictionary<string, Func<List<string[]>>> _grids = new();

    public FakeWindow(FakeDesk desk, string title)
    {
        _desk = desk;
        Title = title;
    }

    public string Title { get; }
    public bool IsOpen => _desk.Windows.Contains(this);
    public Dictionary<string, string> Fields { get; } = new();

    public void Field(string label) => Fields[label] = "";
    public void Button(string text, Action click) => _buttons[text] = click;
    public void List(string label, params string[] items)
    {
        _lists[label] = items;
        Fields[label] = "";
    }
    public void Grid(string label, Func<List<string[]>> rows) => _grids[label] = rows;

    public void SetText(string label, string value)
    {
        if (!Fields.ContainsKey(label) || _lists.ContainsKey(label)) throw new DesktopException($"No input labelled '{label}' in '{Title}'.");
        _desk.Actions.Add($"{label}={value}");
        Fields[label] = value;
    }

    public string GetText(string label) =>
        Fields.TryGetValue(label, out var v) ? v : throw new DesktopException($"No input labelled '{label}' in '{Title}'.");

    public void Select(string label, string item)
    {
        if (!_lists.TryGetValue(label, out var items)) throw new DesktopException($"No input labelled '{label}' in '{Title}'.");
        if (!items.Contains(item)) throw new DesktopException($"{label} has no item '{item}'.");
        _desk.Actions.Add($"{label}={item}");
        Fields[label] = item;
    }

    public void SetChecked(string label, bool value) => throw new DesktopException("No check boxes.");

    public void Click(string buttonText)
    {
        if (!_buttons.TryGetValue(buttonText, out var click)) throw new DesktopException($"No button '{buttonText}' in '{Title}'.");
        _desk.Actions.Add($"[{buttonText}]");
        click();
    }

    public void SendKeys(string keys) => _desk.Actions.Add($"keys {keys}");

    public IReadOnlyList<IReadOnlyList<string>> CopyGrid(string label)
    {
        if (!_grids.TryGetValue(label, out var rows)) throw new DesktopException($"No input labelled '{label}' in '{Title}'.");
        return new[] { new[] { "Name", "Date of birth", "Share %" } }.Concat(rows()).Cast<IReadOnlyList<string>>().ToList();
    }

    public byte[] Capture() => new byte[] { 0x89, 0x50 };
}

public sealed class FakeDialog : IDesktopDialog
{
    private readonly Action<string> _click;

    public FakeDialog(string text, IReadOnlyList<string> buttons, Action<string> click)
    {
        Text = text;
        Buttons = buttons;
        _click = click;
    }

    public string Title => "PolicyDesk";
    public string Text { get; }
    public IReadOnlyList<string> Buttons { get; }

    public void Click(string buttonText)
    {
        if (!Buttons.Contains(buttonText)) throw new DesktopException($"Message has no button '{buttonText}'.");
        _click(buttonText);
    }

    public byte[] Capture() => new byte[] { 0x89, 0x50 };
}
