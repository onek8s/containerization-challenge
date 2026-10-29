using BatchRelease.Core.Model;

namespace BatchRelease.Core.Rules;

public static class SpecificationEvaluator
{
    /// <summary>
    /// Evaluates a laboratory result against its limits. Numeric results are compared to the
    /// limits after rounding to the number of decimals given in the specification (USP &lt;General Notices 7.20&gt;).
    /// Text results keep the outcome reported by the laboratory.
    /// </summary>
    public static ResultOutcome Evaluate(LabResult result)
    {
        if (result.Value is null)
        {
            return string.IsNullOrWhiteSpace(result.TextResult) ? ResultOutcome.Pending : result.Outcome;
        }

        if (result.LowerLimit is null && result.UpperLimit is null)
            return result.Outcome;

        var decimals = Math.Max(Decimals(result.LowerLimit), Decimals(result.UpperLimit));
        var value = Math.Round(result.Value.Value, decimals, MidpointRounding.AwayFromZero);

        if (result.LowerLimit is { } low && value < low)
            return ResultOutcome.Fail;
        if (result.UpperLimit is { } high && value > high)
            return ResultOutcome.Fail;

        return ResultOutcome.Pass;
    }

    private static int Decimals(decimal? limit)
    {
        if (limit is null)
            return 0;
        var bits = decimal.GetBits(limit.Value);
        return (bits[3] >> 16) & 0xFF;
    }
}
