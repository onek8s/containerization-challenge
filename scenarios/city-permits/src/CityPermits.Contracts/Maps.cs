namespace CityPermits.Contracts;

public sealed record ParcelDto(string ParcelId, string? Uprn, string Address, double AreaM2, double Easting, double Northing, double Latitude, double Longitude, string? ZoningCode, string? Ward);

public sealed record IdentifyResultDto(string Layer, string Id, IReadOnlyDictionary<string, string?> Attributes);

public sealed record SiteCheckRequest(string ParcelId, string? FootprintWkt, double BufferM = 3.0);

public sealed record MapExportRequest(string Template, string ParcelId, string? FootprintWkt, string Title, string? Reference, int Dpi = 150, string Format = "png");
