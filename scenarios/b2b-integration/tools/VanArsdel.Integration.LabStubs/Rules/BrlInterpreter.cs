using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace VanArsdel.Integration.LabStubs.Rules
{
    /// <summary>
    /// Interprets the subset of the Business Rules Language our policies use (exported BRL in rules/), so the
    /// lab runs the same rule sets as the BizTalk rule engine: class bindings, compare/and/or/not conditions,
    /// classmember calls with arguments, constants. Like the engine without Update/Assert, every condition is
    /// evaluated against the asserted facts first, then the actions of the matching rules run by priority
    /// (highest first, document order within the same priority).
    /// </summary>
    public class BrlPolicy
    {
        private static readonly XNamespace Brl = "http://schemas.microsoft.com/businessruleslanguage/2002";
        private readonly Dictionary<string, string> _bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<(string Name, int Priority, int Order, XElement Condition, XElement Actions)> _rules = new();

        public string Name { get; private set; }
        public string Version { get; private set; }

        public IReadOnlyList<string> RuleNames => _rules.Select(r => r.Name).ToList();

        public static BrlPolicy Load(string path)
        {
            var doc = XDocument.Load(path);
            var ruleset = doc.Root.Element(Brl + "ruleset") ?? throw new FormatException(path + " has no ruleset.");
            var version = ruleset.Element(Brl + "version");
            var policy = new BrlPolicy
            {
                Name = (string)ruleset.Attribute("name"),
                Version = version == null ? "?" : version.Attribute("major")?.Value + "." + version.Attribute("minor")?.Value
            };

            foreach (var c in ruleset.Element(Brl + "bindings")?.Elements(Brl + "class") ?? Enumerable.Empty<XElement>())
            {
                policy._bindings[(string)c.Attribute("ref")] = (string)c.Attribute("type");
            }

            var order = 0;
            foreach (var rule in ruleset.Elements(Brl + "rule"))
            {
                if (!string.Equals((string)rule.Attribute("active"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var condition = rule.Element(Brl + "if")?.Elements().FirstOrDefault();
                policy._rules.Add(((string)rule.Attribute("name"), int.Parse((string)rule.Attribute("priority") ?? "0", CultureInfo.InvariantCulture), order++, condition, rule.Element(Brl + "then")));
            }

            return policy;
        }

        /// <summary>Runs the policy; returns the names of the rules whose actions ran.</summary>
        public IList<string> Execute(params object[] facts)
        {
            var byRef = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var binding in _bindings)
            {
                var fact = facts.FirstOrDefault(f => f != null && f.GetType().FullName == binding.Value);
                if (fact != null)
                {
                    byRef[binding.Key] = fact;
                }
            }

            // a rule only takes part when every fact it references was asserted
            var agenda = _rules
                .Where(r => References(r.Condition).Concat(References(r.Actions)).All(byRef.ContainsKey))
                .Where(r => r.Condition == null || Convert.ToBoolean(Evaluate(r.Condition, byRef), CultureInfo.InvariantCulture))
                .OrderByDescending(r => r.Priority).ThenBy(r => r.Order)
                .ToList();

            foreach (var rule in agenda)
            {
                foreach (var action in rule.Actions.Elements(Brl + "function"))
                {
                    Evaluate(action, byRef);
                }
            }

            return agenda.Select(r => r.Name).ToList();
        }

        private static IEnumerable<string> References(XElement element)
        {
            return element == null
                ? Enumerable.Empty<string>()
                : element.DescendantsAndSelf(Brl + "classmember").Select(c => (string)c.Attribute("classref")).Distinct();
        }

        private object Evaluate(XElement e, IDictionary<string, object> facts)
        {
            switch (e.Name.LocalName)
            {
                case "and":
                    return e.Elements().All(c => Convert.ToBoolean(Evaluate(c, facts), CultureInfo.InvariantCulture));
                case "or":
                    return e.Elements().Any(c => Convert.ToBoolean(Evaluate(c, facts), CultureInfo.InvariantCulture));
                case "not":
                    return !Convert.ToBoolean(Evaluate(e.Elements().Single(), facts), CultureInfo.InvariantCulture);
                case "compare":
                    return Compare((string)e.Attribute("operator"),
                        Evaluate(e.Element(Brl + "lhs").Elements().Single(), facts),
                        Evaluate(e.Element(Brl + "rhs").Elements().Single(), facts));
                case "function":
                    return Evaluate(e.Elements().Single(), facts);
                case "argument":
                    return Evaluate(e.Elements().Single(), facts);
                case "classmember":
                    return Invoke(e, facts);
                case "constant":
                    return Constant(e.Elements().Single());
                default:
                    throw new NotSupportedException("BRL element <" + e.Name.LocalName + "> is not supported by the lab interpreter.");
            }
        }

        private object Invoke(XElement member, IDictionary<string, object> facts)
        {
            var target = facts[(string)member.Attribute("classref")];
            var name = (string)member.Attribute("member");
            var args = member.Elements(Brl + "argument").Select(a => Evaluate(a, facts)).ToArray();

            var method = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, name);
            }

            var parameters = method.GetParameters();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i]))
                {
                    args[i] = Convert.ChangeType(args[i], parameters[i].ParameterType, CultureInfo.InvariantCulture);
                }
            }

            return method.Invoke(target, args);
        }

        private static object Constant(XElement value)
        {
            var text = value.Value;
            switch (value.Name.LocalName)
            {
                case "string": return text;
                case "boolean": return bool.Parse(text);
                case "int": return int.Parse(text, CultureInfo.InvariantCulture);
                case "decimal": return decimal.Parse(text, CultureInfo.InvariantCulture);
                case "double": return double.Parse(text, CultureInfo.InvariantCulture);
                default: throw new NotSupportedException("BRL constant <" + value.Name.LocalName + "> is not supported.");
            }
        }

        internal static bool Compare(string op, object left, object right)
        {
            int c;
            if (left is string || right is string || left is bool || right is bool)
            {
                c = string.CompareOrdinal(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture));
            }
            else if (left == null || right == null)
            {
                c = left == right ? 0 : left == null ? -1 : 1;
            }
            else
            {
                c = Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
            }

            switch (op)
            {
                case "equal": return c == 0;
                case "not equal": return c != 0;
                case "greater than": return c > 0;
                case "greater than equal": return c >= 0;
                case "less than": return c < 0;
                case "less than or equal": return c <= 0;
                default: throw new NotSupportedException("BRL operator '" + op + "' is not supported.");
            }
        }
    }
}
