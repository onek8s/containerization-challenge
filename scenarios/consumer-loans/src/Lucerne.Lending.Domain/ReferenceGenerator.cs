namespace Lucerne.Lending.Domain;

public static class ReferenceGenerator
{
    /// <summary>LA-yyyy-nnnnnn from the application sequence.</summary>
    public static string Application(int year, long sequence) => $"LA-{year}-{sequence:000000}";

    public static string CollectionCase(long sequence) => $"CC-{sequence:0000000}";
}
