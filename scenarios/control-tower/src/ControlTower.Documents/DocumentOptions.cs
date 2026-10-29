namespace Proseware.ControlTower.Documents;

public sealed class DocumentOptions
{
    public const string Section = "Documents";

    /// <summary>FileStream (SQL Server FILESTREAM, production) or Folder.</summary>
    public string Store { get; set; } = "FileStream";

    /// <summary>Root folder for the Folder store.</summary>
    public string FolderPath { get; set; } = @"D:\ControlTower\Documents";

    /// <summary>Full path of the wkhtmltox native library (wkhtmltopdf 0.12.6).</summary>
    public string WkhtmltoxPath { get; set; } = @"C:\Program Files\wkhtmltopdf\bin\wkhtmltox.dll";

    /// <summary>Public tracking address printed as QR code on documents; {number} is replaced.</summary>
    public string TrackingUrl { get; set; } = "https://track.corp.local/t/{number}";

    /// <summary>How long generated documents are kept (days); 0 keeps them.</summary>
    public int RetentionDays { get; set; } = 3650;

    public IssuerOptions Issuer { get; set; } = new();
}

public sealed class IssuerOptions
{
    public string Name { get; set; } = "Proseware Freight Oy";
    public string Street { get; set; } = "Tikkurilantie 140";
    public string PostalCode { get; set; } = "01530";
    public string City { get; set; } = "Vantaa";
    public string CountryCode { get; set; } = "FI";
    public string VatNumber { get; set; } = "FI01234567";
    public string Phone { get; set; } = "+358 9 4242 0000";
    public string Email { get; set; } = "operations@proseware-freight.example";
}
