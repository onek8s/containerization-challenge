namespace Lucerne.Lending.Domain;

public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundUpToCent(decimal value) => Math.Ceiling(value * 100m) / 100m;
}
