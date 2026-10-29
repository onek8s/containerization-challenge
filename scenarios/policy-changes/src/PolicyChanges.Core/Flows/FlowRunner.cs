using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PolicyChanges.Core.Flows;

/// <summary>Runs a <see cref="FlowDefinition"/> against the PolicyDesk client.</summary>
public sealed class FlowRunner
{
    private readonly IDesktopSession _desktop;
    private readonly ILogger _logger;

    public FlowRunner(IDesktopSession desktop, ILogger<FlowRunner>? logger = null)
    {
        _desktop = desktop;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    public FlowResult Run(FlowDefinition flow, FlowRequest request)
    {
        var context = new FlowContext(request);
        try
        {
            RunSteps(flow, flow.Steps, context);
            if (!context.Variables.ContainsKey("Endorsement"))
                return Result(FlowOutcome.Failed, "Flow finished without an endorsement number.", context);
            return Result(FlowOutcome.Applied, $"Endorsement {context.Variables["Endorsement"]} created.", context);
        }
        catch (FlowRejectedException ex)
        {
            _logger.LogInformation("{Reference}: PolicyDesk refused the change: {Message}", request.Reference, ex.Message);
            CaptureQuietly(context, "rejected");
            RunCleanup(flow, context);
            return Result(FlowOutcome.Rejected, ex.Message, context);
        }
        catch (Exception ex) when (ex is DesktopException or FormatException or TimeoutException)
        {
            _logger.LogWarning(ex, "{Reference}: step {Step} failed", request.Reference, context.CurrentStep);
            CaptureQuietly(context, "failed");
            RunCleanup(flow, context);
            return Result(FlowOutcome.Failed, $"Step {context.CurrentStep}: {ex.Message}", context);
        }
    }

    private static FlowResult Result(FlowOutcome outcome, string message, FlowContext context) =>
        new(outcome, message, new Dictionary<string, string>(context.Variables), context.Evidence.ToList());

    private void RunSteps(FlowDefinition flow, IEnumerable<FlowStep> steps, FlowContext context)
    {
        var index = 0;
        foreach (var step in steps)
        {
            index++;
            var parent = context.CurrentStep;
            context.CurrentStep = parent is null ? $"{index} {step.Action}" : $"{parent}.{index} {step.Action}";
            RunStep(flow, step, context);
            context.CurrentStep = parent;
        }
    }

    private void RunStep(FlowDefinition flow, FlowStep step, FlowContext context)
    {
        var timeout = TimeSpan.FromSeconds(step.TimeoutSeconds ?? flow.DefaultTimeoutSeconds);
        _logger.LogDebug("{Reference}: {Step}", context.Request.Reference, context.CurrentStep);

        switch (step.Action)
        {
            case FlowAction.Window:
                context.Window = _desktop.WaitForWindow(FlowTemplate.Expand(Required(step.Title, "title"), context), timeout);
                break;

            case FlowAction.Set:
                Window(context).SetText(Required(step.Field, "field"), FlowTemplate.Expand(step.Value, context));
                break;

            case FlowAction.Select:
                var item = FlowTemplate.Expand(step.Value, context);
                if (step.Map is not null)
                    item = step.Map.TryGetValue(item, out var mapped) ? mapped : throw new FormatException($"No mapping for '{item}'.");
                Window(context).Select(Required(step.Field, "field"), item);
                break;

            case FlowAction.Check:
                Window(context).SetChecked(Required(step.Field, "field"),
                    bool.Parse(FlowTemplate.Expand(step.Value ?? "true", context)));
                break;

            case FlowAction.Click:
                Window(context).Click(Required(step.Button, "button"));
                break;

            case FlowAction.Keys:
                Window(context).SendKeys(FlowTemplate.Expand(Required(step.Keys, "keys"), context));
                break;

            case FlowAction.Verify:
                var expected = FlowTemplate.Expand(step.Value, context);
                var actual = Window(context).GetText(Required(step.Field, "field"));
                if (!string.Equals(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase))
                    throw new DesktopException($"{step.Field} shows '{actual}', expected '{expected}'.");
                break;

            case FlowAction.Read:
                context.Variables[Required(step.Store, "store")] = Window(context).GetText(Required(step.Field, "field")).Trim();
                break;

            case FlowAction.Capture:
                context.Evidence.Add(new FlowEvidence(Required(step.Name, "name"), Window(context).Capture()));
                break;

            case FlowAction.Dialog:
                HandleDialog(step, timeout, context);
                break;

            case FlowAction.Grid:
                var rows = Window(context).CopyGrid(Required(step.Field, "field"));
                var dataRows = Math.Max(0, rows.Count - 1);
                if (step.Store is not null) context.Variables[step.Store] = dataRows.ToString(CultureInfo.InvariantCulture);
                if (step.ExpectRows is not null)
                {
                    var expectedRows = int.Parse(FlowTemplate.Expand(step.ExpectRows, context), CultureInfo.InvariantCulture);
                    if (expectedRows != dataRows)
                        throw new DesktopException($"{step.Field} has {dataRows} rows, expected {expectedRows}.");
                }
                break;

            case FlowAction.ForEach:
                var previous = context.Item;
                foreach (var line in FlowTemplate.Lines(FlowTemplate.Expand(Required(step.Items, "items"), context)))
                {
                    context.Item = line.Split('|', StringSplitOptions.TrimEntries);
                    RunSteps(flow, step.Steps ?? new(), context);
                }
                context.Item = previous;
                break;

            case FlowAction.When:
                var left = FlowTemplate.Expand(Required(step.Items, "items"), context);
                if (string.Equals(left, FlowTemplate.Expand(step.Is, context), StringComparison.OrdinalIgnoreCase))
                    RunSteps(flow, step.Steps ?? new(), context);
                break;

            case FlowAction.Pause:
                Thread.Sleep(step.Milliseconds ?? 500);
                break;

            default:
                throw new FormatException($"Unknown action {step.Action}.");
        }
    }

    private void HandleDialog(FlowStep step, TimeSpan timeout, FlowContext context)
    {
        var dialog = _desktop.WaitForDialog(timeout);
        if (dialog is null)
        {
            if (step.Optional) return;
            throw new DesktopException("Expected a PolicyDesk message, none appeared.");
        }

        var text = dialog.Text.Replace("\r", "").Trim();
        foreach (var pattern in step.Reject ?? new())
        {
            if (!Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase)) continue;
            context.Evidence.Add(new FlowEvidence("message", dialog.Capture()));
            dialog.Click(dialog.Buttons.FirstOrDefault(b => b is "OK" or "Close") ?? dialog.Buttons.First());
            throw new FlowRejectedException(text);
        }

        foreach (var pattern in step.Accept ?? new())
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(text);
            if (!match.Success) continue;
            foreach (var name in regex.GetGroupNames().Where(n => !int.TryParse(n, out _)))
                if (match.Groups[name].Success) context.Variables[name] = match.Groups[name].Value;
            if (step.Name is not null) context.Evidence.Add(new FlowEvidence(step.Name, dialog.Capture()));
            dialog.Click(step.Button ?? "OK");
            return;
        }

        context.Evidence.Add(new FlowEvidence("message", dialog.Capture()));
        throw new DesktopException($"Unexpected PolicyDesk message: {text}");
    }

    private void RunCleanup(FlowDefinition flow, FlowContext context)
    {
        foreach (var step in flow.Cleanup)
        {
            try
            {
                RunStep(flow, step, context);
            }
            catch (Exception ex) when (ex is DesktopException or FormatException or TimeoutException)
            {
                _logger.LogDebug(ex, "Cleanup step {Action} failed", step.Action);
            }
        }
    }

    private static void CaptureQuietly(FlowContext context, string name)
    {
        try
        {
            if (context.Window is { IsOpen: true } w) context.Evidence.Add(new FlowEvidence(name, w.Capture()));
        }
        catch (DesktopException)
        {
        }
    }

    private static IDesktopWindow Window(FlowContext context) =>
        context.Window ?? throw new FormatException("No window selected; the flow must start with a window step.");

    private static string Required(string? value, string name) =>
        string.IsNullOrEmpty(value) ? throw new FormatException($"Step is missing '{name}'.") : value;

    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
