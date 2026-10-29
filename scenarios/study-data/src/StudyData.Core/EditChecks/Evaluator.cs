using System.Globalization;

namespace StudyData.Core.EditChecks;

/// <summary>Resolves field values for an expression. Names are FIELD or FORM.FIELD.</summary>
public interface IFieldResolver
{
    /// <summary>Returns the raw stored value, or null when the field is blank or does not exist on the subject.</summary>
    string? Resolve(string name);

    /// <summary>Returns the field type so raw values can be converted, or null for an unknown field.</summary>
    Design.FieldType? TypeOf(string name);
}

/// <summary>
/// Evaluates parsed expressions with SQL-like null semantics: a comparison or arithmetic with a blank
/// value is blank, and a check whose result is blank passes (blank fields are the job of the required checks).
/// </summary>
public sealed class Evaluator
{
    private readonly IFieldResolver _fields;
    private readonly DateOnly _today;

    public Evaluator(IFieldResolver fields, DateOnly today)
    {
        _fields = fields;
        _today = today;
    }

    public object? Evaluate(Node node) => node switch
    {
        NumberNode n => n.Value,
        StringNode s => s.Value,
        FieldNode f => Field(f),
        UnaryNode u => Unary(u),
        BinaryNode b => Binary(b),
        CallNode c => Call(c),
        _ => throw new ExpressionException("Unknown node", node.Position)
    };

    private object? Field(FieldNode f)
    {
        switch (f.Name.ToUpperInvariant())
        {
            case "TRUE":
                return true;
            case "FALSE":
                return false;
            case "NULL":
                return null;
        }

        var type = _fields.TypeOf(f.Name) ?? throw new ExpressionException($"Unknown field '{f.Name}'", f.Position);
        var raw = _fields.Resolve(f.Name);
        return Convert(raw, type);
    }

    public static object? Convert(string? raw, Design.FieldType type)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        switch (type)
        {
            case Design.FieldType.Integer:
            case Design.FieldType.Decimal:
            case Design.FieldType.Derived:
                return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : raw;
            case Design.FieldType.Date:
                return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : raw;
            case Design.FieldType.Boolean:
                return raw is "1" or "Y" or "true" or "True";
            case Design.FieldType.Choice:
                return decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : raw;
            default:
                return raw;
        }
    }

    private object? Unary(UnaryNode u)
    {
        var v = Evaluate(u.Operand);
        if (v is null)
        {
            return null;
        }

        return u.Operator switch
        {
            "-" => -AsNumber(v, u),
            "NOT" => !AsBool(v, u),
            _ => throw new ExpressionException($"Unknown operator {u.Operator}", u.Position)
        };
    }

    private object? Binary(BinaryNode b)
    {
        if (b.Operator is "AND" or "OR")
        {
            var l = Evaluate(b.Left);
            if (b.Operator == "AND" && l is false)
            {
                return false;
            }

            if (b.Operator == "OR" && l is true)
            {
                return true;
            }

            var r = Evaluate(b.Right);
            if (l is null || r is null)
            {
                return b.Operator == "AND" && r is false ? false : b.Operator == "OR" && r is true ? true : null;
            }

            return b.Operator == "AND" ? AsBool(l, b) && AsBool(r, b) : AsBool(l, b) || AsBool(r, b);
        }

        var left = Evaluate(b.Left);
        var right = Evaluate(b.Right);
        if (left is null || right is null)
        {
            return null;
        }

        switch (b.Operator)
        {
            case "+":
                return AsNumber(left, b) + AsNumber(right, b);
            case "-":
                if (left is DateOnly dl && right is DateOnly dr)
                {
                    return (decimal)(dl.DayNumber - dr.DayNumber);
                }

                return AsNumber(left, b) - AsNumber(right, b);
            case "*":
                return AsNumber(left, b) * AsNumber(right, b);
            case "/":
                var divisor = AsNumber(right, b);
                return divisor == 0 ? null : AsNumber(left, b) / divisor;
        }

        var cmp = Compare(left, right, b);
        return b.Operator switch
        {
            "=" => cmp == 0,
            "<>" => cmp != 0,
            "<" => cmp < 0,
            "<=" => cmp <= 0,
            ">" => cmp > 0,
            ">=" => cmp >= 0,
            _ => throw new ExpressionException($"Unknown operator {b.Operator}", b.Position)
        };
    }

    private object? Call(CallNode c)
    {
        var args = c.Arguments;
        switch (c.Function)
        {
            case "ISBLANK":
                Arity(c, 1);
                return Evaluate(args[0]) is null;
            case "NOTBLANK":
                Arity(c, 1);
                return Evaluate(args[0]) is not null;
            case "TODAY":
                Arity(c, 0);
                return _today;
            case "ABS":
            {
                Arity(c, 1);
                var v = Evaluate(args[0]);
                return v is null ? null : Math.Abs(AsNumber(v, c));
            }
            case "MIN":
            case "MAX":
            {
                if (args.Count == 0)
                {
                    throw new ExpressionException($"{c.Function} needs arguments", c.Position);
                }

                var values = args.Select(Evaluate).ToList();
                if (values.Any(v => v is null))
                {
                    return null;
                }

                var nums = values.Select(v => AsNumber(v!, c));
                return c.Function == "MIN" ? nums.Min() : nums.Max();
            }
            case "DATEDIFF":
            {
                Arity(c, 2);
                var a = Evaluate(args[0]);
                var b = Evaluate(args[1]);
                if (a is null || b is null)
                {
                    return null;
                }

                return (decimal)(AsDate(a, c).DayNumber - AsDate(b, c).DayNumber);
            }
            case "YEARS":
            {
                Arity(c, 2);
                var a = Evaluate(args[0]);
                var b = Evaluate(args[1]);
                if (a is null || b is null)
                {
                    return null;
                }

                var from = AsDate(a, c);
                var to = AsDate(b, c);
                var years = to.Year - from.Year;
                if (to < from.AddYears(years))
                {
                    years--;
                }

                return (decimal)years;
            }
            case "YEAR":
            {
                Arity(c, 1);
                var a = Evaluate(args[0]);
                return a is null ? null : (decimal)AsDate(a, c).Year;
            }
            case "LEN":
            {
                Arity(c, 1);
                var a = Evaluate(args[0]);
                return a is null ? null : (decimal)System.Convert.ToString(a, CultureInfo.InvariantCulture)!.Length;
            }
            case "IN":
            {
                if (args.Count < 2)
                {
                    throw new ExpressionException("IN needs a value and at least one option", c.Position);
                }

                var v = Evaluate(args[0]);
                if (v is null)
                {
                    return null;
                }

                return args.Skip(1).Select(Evaluate).Any(o => o is not null && Compare(v, o, c) == 0);
            }
            case "IF":
            {
                Arity(c, 3);
                var cond = Evaluate(args[0]);
                return cond is true ? Evaluate(args[1]) : Evaluate(args[2]);
            }
            default:
                throw new ExpressionException($"Unknown function {c.Function}", c.Position);
        }
    }

    private static void Arity(CallNode c, int n)
    {
        if (c.Arguments.Count != n)
        {
            throw new ExpressionException($"{c.Function} takes {n} argument(s)", c.Position);
        }
    }

    private static int Compare(object left, object right, Node at)
    {
        if (left is DateOnly dl && right is DateOnly dr)
        {
            return dl.CompareTo(dr);
        }

        if (left is DateOnly d && right is string s && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var ds))
        {
            return d.CompareTo(ds);
        }

        if (left is bool bl && right is bool br)
        {
            return bl.CompareTo(br);
        }

        if (left is decimal nl && right is decimal nr)
        {
            return nl.CompareTo(nr);
        }

        return string.Compare(
            System.Convert.ToString(left, CultureInfo.InvariantCulture),
            System.Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static decimal AsNumber(object v, Node at) =>
        v switch
        {
            decimal d => d,
            bool b => b ? 1 : 0,
            string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => throw new ExpressionException($"'{v}' is not a number", at.Position)
        };

    private static bool AsBool(object v, Node at) =>
        v switch
        {
            bool b => b,
            decimal d => d != 0,
            _ => throw new ExpressionException($"'{v}' is not a condition", at.Position)
        };

    private static DateOnly AsDate(object v, Node at) =>
        v switch
        {
            DateOnly d => d,
            string s when DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) => d,
            _ => throw new ExpressionException($"'{v}' is not a date", at.Position)
        };
}
