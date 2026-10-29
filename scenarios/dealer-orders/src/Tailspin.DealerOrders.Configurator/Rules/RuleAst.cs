using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.DealerOrders.Configurator.Rules
{
    public interface IRuleScope
    {
        bool Has(string option);
        string Market { get; }
    }

    public abstract class Condition
    {
        public abstract bool Evaluate(IRuleScope scope);
        public abstract IEnumerable<string> ReferencedOptions();
    }

    public sealed class OptionCondition : Condition
    {
        public OptionCondition(string option) { Option = option; }
        public string Option { get; }
        public override bool Evaluate(IRuleScope scope) => scope.Has(Option);
        public override IEnumerable<string> ReferencedOptions() { yield return Option; }
        public override string ToString() => Option;
    }

    public sealed class MarketCondition : Condition
    {
        public MarketCondition(IEnumerable<string> markets) { Markets = markets.Select(m => m.ToUpperInvariant()).ToList(); }
        public IReadOnlyList<string> Markets { get; }
        public override bool Evaluate(IRuleScope scope) => Markets.Contains((scope.Market ?? "").ToUpperInvariant());
        public override IEnumerable<string> ReferencedOptions() => Enumerable.Empty<string>();
        public override string ToString() => "market(" + string.Join(", ", Markets) + ")";
    }

    public sealed class NotCondition : Condition
    {
        public NotCondition(Condition inner) { Inner = inner; }
        public Condition Inner { get; }
        public override bool Evaluate(IRuleScope scope) => !Inner.Evaluate(scope);
        public override IEnumerable<string> ReferencedOptions() => Inner.ReferencedOptions();
        public override string ToString() => "not " + Inner;
    }

    public sealed class BinaryCondition : Condition
    {
        public BinaryCondition(bool isAnd, Condition left, Condition right)
        {
            IsAnd = isAnd;
            Left = left;
            Right = right;
        }

        public bool IsAnd { get; }
        public Condition Left { get; }
        public Condition Right { get; }

        public override bool Evaluate(IRuleScope scope) =>
            IsAnd ? Left.Evaluate(scope) && Right.Evaluate(scope) : Left.Evaluate(scope) || Right.Evaluate(scope);

        public override IEnumerable<string> ReferencedOptions() => Left.ReferencedOptions().Concat(Right.ReferencedOptions());
        public override string ToString() => "(" + Left + (IsAnd ? " and " : " or ") + Right + ")";
    }

    public enum ConsequenceKind
    {
        Requires,
        RequiresAny,
        Excludes,
        Includes,
        Invalid
    }

    public sealed class Consequence
    {
        public Consequence(ConsequenceKind kind, IEnumerable<string> options, string message)
        {
            Kind = kind;
            Options = (options ?? Enumerable.Empty<string>()).ToList();
            Message = message;
        }

        public ConsequenceKind Kind { get; }
        public IReadOnlyList<string> Options { get; }
        public string Message { get; }
    }

    public sealed class CompiledRule
    {
        public CompiledRule(string id, Condition condition, Consequence consequence, string source)
        {
            Id = id;
            Condition = condition;
            Consequence = consequence;
            Source = source;
        }

        public string Id { get; }
        public Condition Condition { get; }
        public Consequence Consequence { get; }
        public string Source { get; }

        public override string ToString() => Id + ": " + Source;
    }
}
