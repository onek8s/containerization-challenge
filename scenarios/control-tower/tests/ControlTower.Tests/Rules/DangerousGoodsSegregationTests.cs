using Proseware.ControlTower.Rules;
using Xunit;

namespace Proseware.ControlTower.Tests.Rules;

public class DangerousGoodsSegregationTests
{
    [Theory]
    [InlineData("1.1D", "3", SegregationResult.Prohibited)]
    [InlineData("3", "1.3G", SegregationResult.Prohibited)]
    [InlineData("1.4S", "3", SegregationResult.Allowed)]
    [InlineData("3", "8", SegregationResult.Allowed)]
    [InlineData("4.1", "5.2", SegregationResult.Conditional)]
    [InlineData("9", "1.2E", SegregationResult.Conditional)]
    [InlineData("2.1", "2.1", SegregationResult.Allowed)]
    [InlineData("X", "3", SegregationResult.Allowed)]
    public void Mixed_loading(string a, string b, SegregationResult expected)
    {
        Assert.Equal(expected, DangerousGoodsSegregation.Check(a, b));
        Assert.Equal(expected, DangerousGoodsSegregation.Check(b, a));
    }

    [Theory]
    [InlineData("6.1", "2810", true)]
    [InlineData("6.2", "3373", true)]
    [InlineData("9", "2590", true)]
    [InlineData("9", "3481", false)]
    [InlineData("3", "1263", false)]
    public void Kept_from_food(string hazardClass, string un, bool expected) =>
        Assert.Equal(expected, DangerousGoodsSegregation.MustBeKeptFromFood(hazardClass, un));
}
