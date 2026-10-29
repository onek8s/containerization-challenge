using StudyData.Core.Design;
using StudyData.Core.EditChecks;

namespace StudyData.Tests;

public class ExpressionTests
{
    private sealed class Fields : IFieldResolver
    {
        private readonly Dictionary<string, (FieldType Type, string? Value)> _values;

        public Fields(params (string Name, FieldType Type, string? Value)[] values) =>
            _values = values.ToDictionary(v => v.Name, v => (v.Type, v.Value), StringComparer.OrdinalIgnoreCase);

        public string? Resolve(string name) => _values.TryGetValue(name, out var v) ? v.Value : null;

        public FieldType? TypeOf(string name) => _values.TryGetValue(name, out var v) ? v.Type : null;
    }

    private static object? Eval(string expression, params (string, FieldType, string?)[] fields) =>
        new Evaluator(new Fields(fields), new DateOnly(2026, 10, 1)).Evaluate(Parser.Parse(expression));

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("-4 + 10 / 4", -1.5)]
    [InlineData("ABS(-3) + MAX(1, 5, 2)", 8)]
    public void Arithmetic(string expression, double expected) =>
        Assert.Equal((decimal)expected, Eval(expression));

    [Fact]
    public void ComparisonOfFields()
    {
        Assert.Equal(true, Eval("SYS > DIA", ("SYS", FieldType.Integer, "128"), ("DIA", FieldType.Integer, "79")));
        Assert.Equal(false, Eval("SYS > DIA", ("SYS", FieldType.Integer, "70"), ("DIA", FieldType.Integer, "79")));
    }

    [Fact]
    public void BlankValuesGiveBlankResult()
    {
        Assert.Null(Eval("SYS > DIA", ("SYS", FieldType.Integer, null), ("DIA", FieldType.Integer, "79")));
        Assert.Equal(true, Eval("ISBLANK(SYS)", ("SYS", FieldType.Integer, "")));
        Assert.Equal(true, Eval("ISBLANK(SYS) OR SYS > 1", ("SYS", FieldType.Integer, null)));
        Assert.Equal(false, Eval("NOTBLANK(SYS) AND SYS > 1", ("SYS", FieldType.Integer, null)));
    }

    [Fact]
    public void Dates()
    {
        Assert.Equal(58m, Eval("YEARS(BRTHDAT, ICDAT)", ("BRTHDAT", FieldType.Date, "1967-09-14"), ("ICDAT", FieldType.Date, "2026-06-02")));
        Assert.Equal(57m, Eval("YEARS(BRTHDAT, ICDAT)", ("BRTHDAT", FieldType.Date, "1968-06-03"), ("ICDAT", FieldType.Date, "2026-06-02")));
        Assert.Equal(3m, Eval("AEENDAT - AESTDAT", ("AESTDAT", FieldType.Date, "2026-06-17"), ("AEENDAT", FieldType.Date, "2026-06-20")));
        Assert.Equal(true, Eval("ICDAT <= TODAY()", ("ICDAT", FieldType.Date, "2026-06-02")));
    }

    [Fact]
    public void ChoicesAndStrings()
    {
        Assert.Equal(true, Eval("IN(EXAUC, 4, 5)", ("EXAUC", FieldType.Choice, "5")));
        Assert.Equal(true, Eval("RSOVR <> 'PD'", ("RSOVR", FieldType.Choice, "SD")));
        Assert.Equal("F", Eval("IF(SEX = 2, 'F', 'M')", ("SEX", FieldType.Choice, "2")));
    }

    [Theory]
    [InlineData("1 +", "Unexpected end")]
    [InlineData("FOO(1)", "Unknown function")]
    [InlineData("'abc", "Unterminated")]
    [InlineData("1 2", "Unexpected '2'")]
    public void Errors(string expression, string message)
    {
        var ex = Assert.Throws<ExpressionException>(() => Eval(expression));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void UnknownFieldIsAnError() =>
        Assert.Throws<ExpressionException>(() => Eval("NOPE > 1"));

    [Fact]
    public void AllSampleStudyExpressionsParse()
    {
        var study = StudyData.Data.Seed.SampleStudy.Create();
        foreach (var form in study.Forms)
        {
            foreach (var check in form.EditChecks)
            {
                Parser.Parse(check.Expression);
                if (check.When is not null)
                {
                    Parser.Parse(check.When);
                }
            }
        }

        foreach (var input in study.Workbooks.SelectMany(w => w.Inputs))
        {
            Parser.Parse(input.Source);
        }
    }
}
