using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Configurator.Rules;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Configurator
{
    public class ConfigurationOutcome
    {
        public ConfigurationOutcome(MachineConfiguration configuration)
        {
            Configuration = configuration;
        }

        public MachineConfiguration Configuration { get; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Options added by defaults or Includes rules, not chosen by the user.</summary>
        public List<string> Added { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>
    /// Completes and validates a machine configuration: applies group defaults and Includes rules until nothing changes,
    /// then checks group cardinality, market restrictions, Requires/Excludes and condition rules.
    /// </summary>
    public class ConfigurationEngine
    {
        private const int MaxPasses = 10;

        private readonly Dictionary<string, IReadOnlyList<CompiledRule>> _compiled = new Dictionary<string, IReadOnlyList<CompiledRule>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();

        public IReadOnlyList<CompiledRule> CompileRules(ProductModel model)
        {
            lock (_sync)
            {
                if (_compiled.TryGetValue(model.ModelCode + "/" + model.ModelYear, out var cached)) return cached;
                var list = new List<CompiledRule>();
                foreach (var rule in model.Rules)
                {
                    switch (rule.Kind)
                    {
                        case OptionRuleKind.Requires:
                            list.Add(new CompiledRule(rule.Id, new OptionCondition(rule.Option.ToUpperInvariant()),
                                new Consequence(rule.Targets.Count > 1 ? ConsequenceKind.RequiresAny : ConsequenceKind.Requires, rule.Targets.Select(t => t.ToUpperInvariant()), rule.Message),
                                rule.Option + " requires " + string.Join(" or ", rule.Targets)));
                            break;
                        case OptionRuleKind.Excludes:
                            foreach (var target in rule.Targets)
                                list.Add(new CompiledRule(rule.Id, new OptionCondition(rule.Option.ToUpperInvariant()),
                                    new Consequence(ConsequenceKind.Excludes, new[] { target.ToUpperInvariant() }, rule.Message),
                                    rule.Option + " excludes " + target));
                            break;
                        case OptionRuleKind.Includes:
                            foreach (var target in rule.Targets)
                                list.Add(new CompiledRule(rule.Id, new OptionCondition(rule.Option.ToUpperInvariant()),
                                    new Consequence(ConsequenceKind.Includes, new[] { target.ToUpperInvariant() }, rule.Message),
                                    rule.Option + " includes " + target));
                            break;
                        case OptionRuleKind.Condition:
                            var compiled = RuleParser.Parse(rule.Id, rule.Expression);
                            if (rule.Message != null && compiled.Consequence.Message == null)
                                compiled = new CompiledRule(compiled.Id, compiled.Condition,
                                    new Consequence(compiled.Consequence.Kind, compiled.Consequence.Options, rule.Message), compiled.Source);
                            list.Add(compiled);
                            break;
                    }
                }

                var unknown = list.SelectMany(r => r.Condition.ReferencedOptions().Concat(r.Consequence.Options))
                    .Where(o => model.FindOption(o) == null).Distinct().ToList();
                if (unknown.Count > 0)
                    throw new InvalidOperationException($"Rules of {model.ModelCode} reference unknown options: {string.Join(", ", unknown)}");

                _compiled[model.ModelCode + "/" + model.ModelYear] = list;
                return list;
            }
        }

        public ConfigurationOutcome Resolve(ProductModel model, IEnumerable<string> selected, string market, DateTime date)
        {
            var chosen = new HashSet<string>(selected.Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0), StringComparer.OrdinalIgnoreCase);
            var outcome = new ConfigurationOutcome(new MachineConfiguration(model.ModelCode, chosen));
            var rules = CompileRules(model);

            foreach (var code in chosen.ToList())
            {
                if (model.FindOption(code) == null)
                {
                    outcome.Errors.Add($"Option {code} does not exist for {model.Name}.");
                    chosen.Remove(code);
                }
            }

            var current = new HashSet<string>(chosen, StringComparer.OrdinalIgnoreCase);
            var scope = new Scope(current, market);

            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var changed = false;

                foreach (var group in model.OptionGroups.Where(g => g.Kind == OptionGroupKind.SingleRequired))
                {
                    if (group.Options.Any(o => current.Contains(o.Code))) continue;
                    var def = group.Options.FirstOrDefault(o => o.IsDefault && o.IsAvailableIn(market)) ?? group.Options.FirstOrDefault(o => o.IsAvailableIn(market));
                    if (def == null) continue;
                    current.Add(def.Code);
                    outcome.Added.Add(def.Code);
                    changed = true;
                }

                foreach (var rule in rules.Where(r => r.Consequence.Kind == ConsequenceKind.Includes))
                {
                    if (!rule.Condition.Evaluate(scope)) continue;
                    foreach (var target in rule.Consequence.Options)
                    {
                        if (current.Contains(target)) continue;
                        var group = model.GroupOf(target);
                        if (group != null && group.Kind != OptionGroupKind.Multiple)
                        {
                            // Included option replaces a default in a single-choice group, never a user choice.
                            var replaced = group.Options.Where(o => current.Contains(o.Code) && !chosen.Contains(o.Code)).Select(o => o.Code).ToList();
                            foreach (var r in replaced) { current.Remove(r); outcome.Added.Remove(r); }
                            if (group.Options.Any(o => current.Contains(o.Code))) continue;
                        }
                        current.Add(target);
                        outcome.Added.Add(target);
                        changed = true;
                    }
                }

                if (!changed) break;
                if (pass == MaxPasses - 1) outcome.Errors.Add("Configuration rules do not settle; contact the sales desk.");
            }

            if (!model.IsAvailableIn(market))
                outcome.Errors.Add($"{model.Name} is not sold in market {market}.");
            if (!model.IsValidOn(date))
                outcome.Errors.Add($"{model.Name} model year {model.ModelYear} cannot be ordered on {date:yyyy-MM-dd}.");

            foreach (var group in model.OptionGroups)
            {
                var count = group.Options.Count(o => current.Contains(o.Code));
                if (group.Kind == OptionGroupKind.SingleRequired && count == 0)
                    outcome.Errors.Add($"Select one option in {group.Name}.");
                if (group.Kind != OptionGroupKind.Multiple && count > 1)
                    outcome.Errors.Add($"Only one option can be selected in {group.Name}: {string.Join(", ", group.Options.Where(o => current.Contains(o.Code)).Select(o => o.Code))}.");
            }

            foreach (var code in current)
            {
                var option = model.FindOption(code);
                if (option != null && !option.IsAvailableIn(market))
                    outcome.Errors.Add($"{option.Name} ({code}) is not available in market {market}.");
            }

            foreach (var rule in rules)
            {
                if (!rule.Condition.Evaluate(scope)) continue;
                var c = rule.Consequence;
                switch (c.Kind)
                {
                    case ConsequenceKind.Requires:
                        if (!current.Contains(c.Options[0]))
                            outcome.Errors.Add(c.Message ?? $"{Describe(rule)} requires {Name(model, c.Options[0])}.");
                        break;
                    case ConsequenceKind.RequiresAny:
                        if (!c.Options.Any(current.Contains))
                            outcome.Errors.Add(c.Message ?? $"{Describe(rule)} requires one of {string.Join(", ", c.Options.Select(o => Name(model, o)))}.");
                        break;
                    case ConsequenceKind.Excludes:
                        if (current.Contains(c.Options[0]))
                            outcome.Errors.Add(c.Message ?? $"{Describe(rule)} cannot be combined with {Name(model, c.Options[0])}.");
                        break;
                    case ConsequenceKind.Invalid:
                        outcome.Errors.Add(c.Message ?? $"The combination {rule.Condition} is not allowed.");
                        break;
                }
            }

            if (current.Count > 0)
            {
                var extra = current.Select(model.FindOption).Where(o => o != null && o.ExtraLeadTimeWeeks > 0).ToList();
                foreach (var o in extra)
                    outcome.Warnings.Add($"{o.Name} adds {o.ExtraLeadTimeWeeks} weeks to the delivery time.");
            }

            var final = new MachineConfiguration(model.ModelCode, current);
            var result = new ConfigurationOutcome(final);
            result.Errors.AddRange(outcome.Errors.Distinct());
            result.Warnings.AddRange(outcome.Warnings.Distinct());
            result.Added.AddRange(outcome.Added.Where(current.Contains).Distinct());
            return result;
        }

        private static string Describe(CompiledRule rule) =>
            rule.Condition is OptionCondition oc ? oc.Option : rule.Condition.ToString();

        private static string Name(ProductModel model, string code)
        {
            var option = model.FindOption(code);
            return option == null ? code : $"{option.Name} ({code})";
        }

        private sealed class Scope : IRuleScope
        {
            private readonly HashSet<string> _options;

            public Scope(HashSet<string> options, string market)
            {
                _options = options;
                Market = market;
            }

            public bool Has(string option) => _options.Contains(option);
            public string Market { get; }
        }
    }
}
