namespace ExpenseClaims.Jobs.Cards;

public sealed class CardImportOptions
{
    public const string SectionName = "CardImport";

    /// <summary>Folder where the card company's daily transaction files are delivered.</summary>
    public string InboundFolder { get; set; } = @"D:\Interfaces\Cards\Inbound";

    public string FilePattern { get; set; } = "TRX_*.csv";

    /// <summary>Culture of numbers and dates in the file. The card company sends Finnish formatting (1 234,50 and 31.12.2022).</summary>
    public string Culture { get; set; } = "fi-FI";
}
