namespace FieldService.Web.Services;

public class PortalOptions
{
    public const string Section = "Portal";

    public string CompanyName { get; set; } = "Field Service";

    public string PhotoRoot { get; set; } = "";

    public long MaxPhotoBytes { get; set; } = 10 * 1024 * 1024;

    public bool SeedSampleData { get; set; }
}
