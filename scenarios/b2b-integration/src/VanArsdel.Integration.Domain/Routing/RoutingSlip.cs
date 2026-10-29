using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Domain.Routing
{
    public enum RoutingStepKind
    {
        Transform = 1,
        Send = 2,
        Archive = 3,
        Track = 4,
        Notify = 5
    }

    public class RoutingStep
    {
        public RoutingStep(RoutingStepKind kind, string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("A routing step needs a target.", "target");
            }

            Kind = kind;
            Target = target.Trim();
        }

        public RoutingStepKind Kind { get; private set; }
        public string Target { get; private set; }

        public override string ToString()
        {
            return Prefix(Kind) + ":" + Target;
        }

        internal static string Prefix(RoutingStepKind kind)
        {
            switch (kind)
            {
                case RoutingStepKind.Transform:
                    return "MAP";
                case RoutingStepKind.Send:
                    return "SEND";
                case RoutingStepKind.Archive:
                    return "ARCHIVE";
                case RoutingStepKind.Track:
                    return "TRACK";
                default:
                    return "NOTIFY";
            }
        }

        internal static RoutingStepKind Parse(string prefix)
        {
            switch (prefix.Trim().ToUpperInvariant())
            {
                case "MAP":
                    return RoutingStepKind.Transform;
                case "SEND":
                    return RoutingStepKind.Send;
                case "ARCHIVE":
                    return RoutingStepKind.Archive;
                case "TRACK":
                    return RoutingStepKind.Track;
                case "NOTIFY":
                    return RoutingStepKind.Notify;
                default:
                    throw new FormatException("Unknown routing step '" + prefix + "'.");
            }
        }
    }

    /// <summary>
    /// Itinerary carried in the VanArsdel.RoutingSlip context property, ESB Toolkit style: a list of steps and the
    /// index of the current one. Text form: "2|MAP:Despatch_to_X12_856;SEND:AS2.HARTWELL;ARCHIVE:outbound".
    /// The dispatcher orchestration executes the current step, advances and publishes the message again.
    /// </summary>
    public class RoutingSlip
    {
        private readonly List<RoutingStep> _steps;

        public RoutingSlip(IEnumerable<RoutingStep> steps, int currentIndex = 0)
        {
            _steps = (steps ?? Enumerable.Empty<RoutingStep>()).ToList();
            if (currentIndex < 0 || currentIndex > _steps.Count)
            {
                throw new ArgumentOutOfRangeException("currentIndex");
            }

            CurrentIndex = currentIndex;
        }

        public IList<RoutingStep> Steps
        {
            get { return _steps.AsReadOnly(); }
        }

        public int CurrentIndex { get; private set; }

        public bool IsComplete
        {
            get { return CurrentIndex >= _steps.Count; }
        }

        public RoutingStep Current
        {
            get { return IsComplete ? null : _steps[CurrentIndex]; }
        }

        public RoutingSlip Advance()
        {
            if (IsComplete)
            {
                throw new InvalidOperationException("The routing slip is already complete.");
            }

            return new RoutingSlip(_steps, CurrentIndex + 1);
        }

        public static RoutingSlip Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new RoutingSlip(null);
            }

            var index = 0;
            var body = text.Trim();
            var bar = body.IndexOf('|');
            if (bar > 0)
            {
                index = int.Parse(body.Substring(0, bar), System.Globalization.CultureInfo.InvariantCulture);
                body = body.Substring(bar + 1);
            }

            var steps = body.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s =>
                {
                    var colon = s.IndexOf(':');
                    if (colon <= 0)
                    {
                        throw new FormatException("Routing step '" + s + "' has no kind.");
                    }

                    return new RoutingStep(RoutingStep.Parse(s.Substring(0, colon)), s.Substring(colon + 1));
                });

            return new RoutingSlip(steps, index);
        }

        public override string ToString()
        {
            return CurrentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + string.Join(";", _steps.Select(s => s.ToString()));
        }
    }
}
