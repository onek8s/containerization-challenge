using System.Globalization;
using Newtonsoft.Json;
using Polly;
using RestSharp;

namespace CityPermits.Integration;

public sealed record AddressMatch(string Uprn, string Address, string Postcode, double Easting, double Northing, string? ParcelId, string? Ward);

public interface IAddressRegister
{
    IReadOnlyList<AddressMatch> Search(string text, int max = 20);
    AddressMatch? Get(string uprn);
}

public sealed class AddressRegisterOptions
{
    /// <summary>Api (national address service) or File (CSV extract).</summary>
    public string Mode { get; set; } = "Api";
    public string BaseUrl { get; set; } = "https://api.addressregister.gov.uk/v1/";
    public string ApiKey { get; set; } = "";
    public string LocalAuthorityCode { get; set; } = "E07000105";
    public string FilePath { get; set; } = "../../samples/address-register.csv";
}

public sealed class AddressRegisterApi : IAddressRegister
{
    private readonly RestClient _client;
    private readonly AddressRegisterOptions _options;
    private readonly ISyncPolicy _retry = Policy.Handle<Exception>().WaitAndRetry(3, i => TimeSpan.FromMilliseconds(200 * i));

    public AddressRegisterApi(AddressRegisterOptions options)
    {
        _options = options;
        _client = new RestClient(options.BaseUrl) { Timeout = 10000 };
        _client.AddDefaultHeader("X-Api-Key", options.ApiKey);
    }

    public IReadOnlyList<AddressMatch> Search(string text, int max = 20) => _retry.Execute(() =>
    {
        var req = new RestRequest("find", Method.GET).AddQueryParameter("query", text).AddQueryParameter("lad", _options.LocalAuthorityCode)
            .AddQueryParameter("maxresults", max.ToString(CultureInfo.InvariantCulture)).AddQueryParameter("srs", "EPSG:27700");
        var res = _client.Execute(req);
        if (!res.IsSuccessful) throw new InvalidOperationException($"Address register {(int)res.StatusCode}");
        dynamic body = JsonConvert.DeserializeObject(res.Content)!;
        var list = new List<AddressMatch>();
        foreach (var r in body.results)
            list.Add(new AddressMatch((string)r.uprn, (string)r.address, (string)r.postcode, (double)r.x, (double)r.y, null, (string?)r.ward));
        return (IReadOnlyList<AddressMatch>)list;
    });

    public AddressMatch? Get(string uprn) => _retry.Execute(() =>
    {
        var res = _client.Execute(new RestRequest("uprn/{uprn}", Method.GET).AddUrlSegment("uprn", uprn).AddQueryParameter("srs", "EPSG:27700"));
        if ((int)res.StatusCode == 404) return null;
        dynamic r = JsonConvert.DeserializeObject(res.Content)!;
        return new AddressMatch((string)r.uprn, (string)r.address, (string)r.postcode, (double)r.x, (double)r.y, null, (string?)r.ward);
    });
}

public sealed class AddressRegisterFile : IAddressRegister
{
    private readonly List<AddressMatch> _rows;

    public AddressRegisterFile(string path)
    {
        _rows = File.ReadLines(path).Skip(1).Select(l =>
        {
            var q = l.IndexOf('"');
            var q2 = l.IndexOf('"', q + 1);
            var address = l[(q + 1)..q2];
            var parts = (l[..q] + "X" + l[(q2 + 1)..]).Split(',');
            return new AddressMatch(parts[0], address, parts[2], double.Parse(parts[3], CultureInfo.InvariantCulture),
                double.Parse(parts[4], CultureInfo.InvariantCulture), parts[5], parts[6]);
        }).ToList();
    }

    public IReadOnlyList<AddressMatch> Search(string text, int max = 20) =>
        _rows.Where(r => r.Address.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Postcode.Replace(" ", "").Contains(text.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            .Take(max).ToList();

    public AddressMatch? Get(string uprn) => _rows.FirstOrDefault(r => r.Uprn == uprn);
}
