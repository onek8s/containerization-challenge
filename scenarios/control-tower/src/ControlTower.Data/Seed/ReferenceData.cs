using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Fleet;
using Proseware.ControlTower.Domain.Locations;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;

namespace Proseware.ControlTower.Data.Seed;

/// <summary>Master data of the Proseware network as delivered with the first production release.</summary>
public static class ReferenceData
{
    private const string Fle = "FLE Standard Time";
    private const string Cet = "W. Europe Standard Time";
    private const string CetPl = "Central European Standard Time";
    private const string Uk = "GMT Standard Time";
    private const string China = "China Standard Time";
    private const string UsEast = "Eastern Standard Time";

    public static IReadOnlyList<Location> Locations() => new List<Location>
    {
        Loc("FIVAN", "Proseware Terminal Vantaa", LocationKind.Terminal, "Tikkurilantie 140", "01530", "Vantaa", "FI", 60.3133, 24.9637, Fle, 400),
        Loc("FITKU", "Proseware Terminal Turku", LocationKind.Terminal, "Satamakatu 12", "20100", "Turku", "FI", 60.4370, 22.2207, Fle, 400),
        Loc("SEGOT", "Proseware Cross-dock Gothenburg", LocationKind.Terminal, "Logistikvägen 8", "41878", "Göteborg", "SE", 57.7180, 11.9310, Cet, 400),
        Loc("EETLL", "Proseware Terminal Tallinn", LocationKind.Terminal, "Peterburi tee 46", "11415", "Tallinn", "EE", 59.4280, 24.8350, Fle, 400),
        Loc("FIVUO", "Port of Helsinki Vuosaari", LocationKind.Seaport, "Provianttikatu 5", "00980", "Helsinki", "FI", 60.2086, 25.1903, Fle, 1500),
        Loc("DEHAM", "Port of Hamburg CTA", LocationKind.Seaport, "Bei St. Annen 1", "20457", "Hamburg", "DE", 53.5228, 9.9370, Cet, 2000),
        Loc("NLRTM", "Rotterdam Maasvlakte", LocationKind.Seaport, "Europaweg 875", "3199 LD", "Rotterdam", "NL", 51.9490, 4.0520, Cet, 2500),
        Loc("CNSHA", "Shanghai Yangshan Deep Water Port", LocationKind.Seaport, "Shengdong Rd 1", "201308", "Shanghai", "CN", 30.6280, 122.0650, China, 3000, eu: false),
        Loc("USNYC", "Port Newark Container Terminal", LocationKind.Seaport, "241 Calcutta St", "07114", "Newark", "US", 40.6895, -74.1420, UsEast, 2500, eu: false),
        Loc("FIHEL-AP", "Helsinki Airport Cargo", LocationKind.Airport, "Rahtitie 1", "01530", "Vantaa", "FI", 60.3250, 24.9690, Fle, 1200),
        Loc("DEFRA-AP", "Frankfurt Airport CargoCity Süd", LocationKind.Airport, "Gebäude 573", "60549", "Frankfurt", "DE", 50.0300, 8.5800, Cet, 1500),
        Loc("CNPVG-AP", "Shanghai Pudong Airport Cargo", LocationKind.Airport, "Yingbin Ave 6000", "201207", "Shanghai", "CN", 31.1520, 121.8000, China, 1500, eu: false),
        Loc("NOSVB", "Svinesund border crossing", LocationKind.BorderCrossing, "E6", "1789", "Halden", "NO", 59.0942, 11.2683, Cet, 800, eu: false),
        Loc("FITRE-NWF", "Northwind Foods Tampere", LocationKind.CustomerSite, "Hatanpään valtatie 30", "33100", "Tampere", "FI", 61.4870, 23.7660, Fle, 300, customer: 1),
        Loc("FIOUL-FAB", "Fabrikam Machinery Oulu", LocationKind.CustomerSite, "Teknologiantie 4", "90590", "Oulu", "FI", 65.0580, 25.4430, Fle, 300, customer: 3),
        Loc("FIJYV-CPH", "Contoso Pharma Jyväskylä", LocationKind.CustomerSite, "Ohjelmakaari 10", "40500", "Jyväskylä", "FI", 62.2370, 25.7570, Fle, 300, customer: 2),
        Loc("FILAH-WGP", "Woodgrove Paints Lahti", LocationKind.CustomerSite, "Askonkatu 9", "15100", "Lahti", "FI", 60.9890, 25.6800, Fle, 300, customer: 7),
        Loc("FIESP-WWI", "Wide World Importers Espoo", LocationKind.CustomerSite, "Juvan teollisuuskatu 15", "02920", "Espoo", "FI", 60.2380, 24.7140, Fle, 300, customer: 5),
        Loc("SESTO-AWN", "Adventure Works Nordic Stockholm", LocationKind.CustomerSite, "Lagervägen 3", "12634", "Hägersten", "SE", 59.2980, 17.9800, Cet, 300, customer: 4),
        Loc("SEMMA-TST", "Tailspin Toys Malmö", LocationKind.CustomerSite, "Industrigatan 21", "21214", "Malmö", "SE", 55.6200, 13.0400, Cet, 300, customer: 6),
        Loc("NOOSL-ASH", "Alpine Ski House Oslo", LocationKind.CustomerSite, "Alnabruveien 15", "1081", "Oslo", "NO", 59.9330, 10.8440, Cet, 300, customer: 8, eu: false),
        Loc("DEMUC-CWG", "Coho Winery München", LocationKind.CustomerSite, "Lilienthalallee 40", "80939", "München", "DE", 48.1880, 11.5960, Cet, 300, customer: 9),
        Loc("PLPOZ-TRR", "Trey Research Poznań", LocationKind.CustomerSite, "ul. Gdyńska 31", "61-016", "Poznań", "PL", 52.4210, 16.9600, CetPl, 300, customer: 11),
        Loc("GBBHM-LUC", "Lucerne Publishing Birmingham", LocationKind.CustomerSite, "Fort Parkway 2", "B24 9FP", "Birmingham", "GB", 52.5150, -1.8150, Uk, 300, customer: 10, eu: false),
        Loc("CNSZX-SUP", "Shenzhen supplier consolidation", LocationKind.CarrierDepot, "Pingshan 88", "518118", "Shenzhen", "CN", 22.6900, 114.3500, China, 500, eu: false),
    };

    public static IReadOnlyList<Customer> Customers(DateTimeOffset now) => new List<Customer>
    {
        Cust(1, "NWF", "Northwind Foods Oy", "FI", "FI24871234", "logistics@northwind-foods.example", portal: true, sla: 1, discount: 6m),
        Cust(2, "CPH", "Contoso Pharma Oy", "FI", "FI10522231", "supply@contoso-pharma.example", portal: true, sla: 1, discount: 4m, brokerage: true),
        Cust(3, "FAB", "Fabrikam Machinery Oy", "FI", "FI08134560", "shipping@fabrikam.example", portal: true, sla: 2, discount: 8m, brokerage: true),
        Cust(4, "AWN", "Adventure Works Nordic AB", "SE", "SE556677889901", "order@adventure-works.example", portal: true, sla: 2),
        Cust(5, "WWI", "Wide World Importers Oy", "FI", "FI22339981", "import@wideworldimporters.example", portal: true, sla: 2, discount: 10m, brokerage: true),
        Cust(6, "TST", "Tailspin Toys AB", "SE", "SE559012345601", "logistik@tailspintoys.example", portal: false, sla: 3),
        Cust(7, "WGP", "Woodgrove Paints Oy", "FI", "FI07771230", "toimitukset@woodgrove-paints.example", portal: true, sla: 2),
        Cust(8, "ASH", "Alpine Ski House AS", "NO", "NO987654321", "frakt@alpineskihouse.example", portal: true, sla: 3, brokerage: true),
        Cust(9, "CWG", "Coho Winery GmbH", "DE", "DE812345678", "versand@cohowinery.example", portal: false, sla: 3),
        Cust(10, "LUC", "Lucerne Publishing Ltd", "GB", "GB123456789", "distribution@lucernepublishing.example", portal: true, sla: 4, brokerage: true),
        Cust(11, "TRR", "Trey Research Sp. z o.o.", "PL", "PL7781234567", "logistyka@treyresearch.example", portal: false, sla: 3),
        Cust(12, "PRO", "Proseware Freight internal", "FI", "FI01234567", "ops@proseware-freight.example", portal: false, sla: 2),
    }.Select(c => { c.CreatedUtc = now.UtcDateTime.AddYears(-2); return c; }).ToList();

    public static IReadOnlyList<Carrier> Carriers() => new List<Carrier>
    {
        new() { Id = 1, Code = "NRDL", Name = "Nordic Linehaul Oy", Mode = TransportMode.Road, Integration = CarrierIntegration.EdifactSftp, EdiPartnerId = "003712345678", EdiQualifier = "30", TrackingUrlTemplate = "https://track.nordic-linehaul.example/?ref={ref}" },
        new() { Id = 2, Code = "BALX", Name = "Baltic Express Trucking AS", Mode = TransportMode.Road, Integration = CarrierIntegration.RestApi, ApiBaseUrl = "https://api.balticexpress.example/v2/", TrackingUrlTemplate = "https://balticexpress.example/track/{ref}" },
        new() { Id = 3, Code = "POHJ", Name = "Pohjolan Kuljetus Oy", Mode = TransportMode.Road, Integration = CarrierIntegration.Manual },
        new() { Id = 4, Code = "VNRL", Name = "Vänern Linjer AB", Mode = TransportMode.Road, Integration = CarrierIntegration.EdifactSftp, EdiPartnerId = "7350012340007", EdiQualifier = "14" },
        new() { Id = 5, Code = "OCNX", Name = "Oceanix Container Line", Mode = TransportMode.Sea, Integration = CarrierIntegration.EdifactSftp, EdiPartnerId = "OCNXEDI", EdiQualifier = "ZZZ", TrackingUrlTemplate = "https://www.oceanix.example/tracking?bl={ref}" },
        new() { Id = 6, Code = "NSFL", Name = "North Sea Feeder Line", Mode = TransportMode.Sea, Integration = CarrierIntegration.EdifactSftp, EdiPartnerId = "NSFEEDER", EdiQualifier = "ZZZ" },
        new() { Id = 7, Code = "AFX", Name = "Aerofreight Cargo", Mode = TransportMode.Air, Integration = CarrierIntegration.RestApi, ApiBaseUrl = "https://cargo-api.aerofreight.example/", TrackingUrlTemplate = "https://cargo.aerofreight.example/awb/{ref}" },
        new() { Id = 8, Code = "PRCL", Name = "ParcelNord", Mode = TransportMode.Road, Integration = CarrierIntegration.RestApi, ApiBaseUrl = "https://api.parcelnord.example/", TrackingUrlTemplate = "https://parcelnord.example/t/{ref}" },
    };

    public static IReadOnlyList<Vehicle> Vehicles()
    {
        var plates = new[] { "KTP-412", "LXR-118", "MNB-903", "JTK-227", "PRF-501", "PRF-502", "PRF-503", "PRF-504", "ZKL-660", "GHV-341", "UTA-772", "RSE-145", "BCO-090", "PRF-505" };
        return plates.Select((p, i) => new Vehicle
        {
            Id = i + 1,
            Plate = p,
            CarrierId = p.StartsWith("PRF", StringComparison.Ordinal) ? null : (i % 3 == 0 ? 1 : i % 3 == 1 ? 3 : 2),
            TelematicsUnitId = $"tu-{1001 + i}",
            HasReefer = i % 4 == 0,
            AdrCertified = i % 3 != 2,
            MaxPayloadKg = i % 5 == 4 ? 12_000m : 24_000m,
            LoadingMeters = i % 5 == 4 ? 7.2m : 13.6m
        }).ToList();
    }

    public static IReadOnlyList<RateCard> RateCards(DateTime today)
    {
        var from = new DateTime(today.Year - 1, 1, 1);
        var to = new DateTime(today.Year + 1, 12, 31);
        var list = new List<RateCard>();
        var id = 1;
        void Add(TransportMode mode, string o, string d, int? carrier, decimal min, int days, decimal fuel, params (decimal kg, decimal price)[] breaks)
        {
            var card = new RateCard { Id = id++, Mode = mode, OriginCountry = o, DestinationCountry = d, CarrierId = carrier, MinimumCharge = min, TransitDays = days, FuelSurchargePercent = fuel, ValidFrom = from, ValidTo = to };
            var b = 1;
            card.Breaks = breaks.Select(x => new RateBreak { Id = card.Id * 10 + b++, RateCardId = card.Id, FromKg = x.kg, PricePer100Kg = x.price }).ToList();
            list.Add(card);
        }

        Add(TransportMode.Road, "FI", "FI", 1, 38m, 1, 14.5m, (0, 21m), (500, 14m), (2500, 9.5m), (10000, 6.2m));
        Add(TransportMode.Road, "FI", "FI", 3, 35m, 2, 12m, (0, 19m), (500, 13.5m), (2500, 9.9m), (10000, 6.8m));
        Add(TransportMode.Road, "FI", "FI", 8, 12m, 1, 9m, (0, 48m), (30, 32m));
        Add(TransportMode.Road, "FI", "SE", 4, 65m, 2, 15m, (0, 34m), (500, 22m), (2500, 15m), (10000, 9.8m));
        Add(TransportMode.Road, "SE", "FI", 4, 65m, 2, 15m, (0, 33m), (500, 21m), (2500, 14.5m), (10000, 9.5m));
        Add(TransportMode.Road, "FI", "EE", 2, 55m, 1, 13m, (0, 29m), (500, 18m), (2500, 12m));
        Add(TransportMode.Road, "FI", "DE", 2, 120m, 4, 16m, (0, 58m), (500, 39m), (2500, 26m), (10000, 17m));
        Add(TransportMode.Road, "FI", "PL", 2, 110m, 3, 16m, (0, 52m), (500, 35m), (2500, 23m), (10000, 15m));
        Add(TransportMode.Road, "FI", "NO", 4, 140m, 4, 15m, (0, 61m), (500, 42m), (2500, 28m));
        Add(TransportMode.Road, "FI", "GB", 1, 190m, 5, 17m, (0, 79m), (500, 55m), (2500, 37m));
        Add(TransportMode.Sea, "CN", "FI", 5, 280m, 38, 22m, (0, 19m), (5000, 12m));
        Add(TransportMode.Sea, "FI", "CN", 5, 260m, 40, 22m, (0, 18m), (5000, 11m));
        Add(TransportMode.Sea, "FI", "US", 6, 310m, 21, 20m, (0, 24m), (5000, 15m));
        Add(TransportMode.Sea, "DE", "FI", 6, 150m, 5, 18m, (0, 14m), (5000, 9m));
        Add(TransportMode.Air, "FI", "CN", 7, 95m, 3, 28m, (0, 520m), (45, 410m), (100, 365m), (300, 330m), (500, 298m));
        Add(TransportMode.Air, "CN", "FI", 7, 95m, 3, 28m, (0, 560m), (45, 445m), (100, 390m), (300, 350m), (500, 315m));
        Add(TransportMode.Air, "FI", "US", 7, 90m, 2, 27m, (0, 480m), (45, 395m), (100, 340m), (300, 305m));
        Add(TransportMode.Air, "DE", "FI", 7, 70m, 1, 25m, (0, 290m), (45, 230m), (100, 190m));
        return list;
    }

    private static Location Loc(string code, string name, LocationKind kind, string street, string postal, string city, string country,
        double lat, double lon, string tz, int radius, int? customer = null, bool eu = true) => new()
    {
        Code = code,
        Name = name,
        Kind = kind,
        Street = street,
        PostalCode = postal,
        City = city,
        CountryCode = country,
        Position = GeoPoints.Create(lat, lon),
        TimeZoneId = tz,
        GeofenceRadiusMeters = radius,
        CustomerId = customer,
        IsEuCustomsTerritory = eu,
        OpensAt = kind == LocationKind.Terminal ? new TimeSpan(5, 0, 0) : new TimeSpan(7, 0, 0),
        ClosesAt = kind == LocationKind.Terminal ? new TimeSpan(22, 0, 0) : new TimeSpan(16, 0, 0)
    };

    private static Customer Cust(int id, string code, string name, string country, string vat, string email, bool portal, int sla,
        decimal discount = 0m, bool brokerage = false) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        CountryCode = country,
        VatNumber = vat,
        Eori = vat.Replace("FI", "FI", StringComparison.Ordinal),
        ContactEmail = email,
        PortalEnabled = portal,
        DomesticSlaDays = sla,
        DiscountPercent = discount,
        CustomsBrokerageAgreed = brokerage,
        InvoiceCurrency = country switch { "SE" => "SEK", "NO" => "NOK", "GB" => "GBP", "PL" => "PLN", _ => "EUR" }
    };
}
