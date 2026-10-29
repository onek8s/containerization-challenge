namespace Fabrikam.Housing.Documents
{
    /// <summary>Bound from the "Documents" configuration section.</summary>
    public class DocumentsOptions
    {
        /// <summary>Wkhtmltopdf (letters rendered from HTML by libwkhtmltox) or Basic (iTextSharp HTML worker).</summary>
        public string PdfEngine { get; set; } = "Wkhtmltopdf";
        /// <summary>Folder with libwkhtmltox.dll, relative to the application folder.</summary>
        public string NativeLibraryFolder { get; set; } = "native";
        /// <summary>Letter templates (HTML with {{Placeholders}}).</summary>
        public string TemplateFolder { get; set; } = "Templates";
        public string LogoFile { get; set; } = @"Templates\fabrikam-logo.png";
    }
}
