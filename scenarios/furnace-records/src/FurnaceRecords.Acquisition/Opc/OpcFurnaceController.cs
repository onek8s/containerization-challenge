using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace FurnaceRecords.Acquisition.Opc;

/// <summary>Furnace controller state from the furnace HMI's OPC server.</summary>
[SupportedOSPlatform("windows")]
internal sealed class OpcFurnaceController : IFurnaceController
{
    private readonly FurnaceAcquisitionOptions _options;
    private readonly int _updateRateMs;
    private readonly ILogger _logger;
    private OpcDaClient? _client;

    public OpcFurnaceController(FurnaceAcquisitionOptions options, int updateRateMs, ILogger logger)
    {
        _options = options;
        _updateRateMs = updateRateMs;
        _logger = logger;
    }

    private string Tag(string name) => string.IsNullOrEmpty(_options.TagPrefix) ? name : $"{_options.TagPrefix}.{name}";

    private IReadOnlyList<string> Items()
    {
        var t = _options.Tags;
        return new[] { t.CycleActive, t.RecipeNumber, t.Setpoint, t.ControlTemperature, t.DoorOpen, t.QuenchDown, t.AlarmWord, t.OverTemperature }
            .Select(Tag).ToArray();
    }

    public void Connect()
    {
        _client?.Dispose();
        var clsid = Guid.TryParse(_options.OpcClsid, out var g) ? g : (Guid?)null;
        _client = new OpcDaClient(_options.OpcHost, _options.OpcProgId, clsid);
        _client.Connect(Items(), _updateRateMs);
        _logger.LogInformation("Connected to {ProgId} on {Host} ({Vendor}) for furnace {Furnace}",
            _options.OpcProgId, _options.OpcHost, _client.VendorInfo, _options.Code);
        foreach (var (item, error) in _client.RejectedItems())
        {
            _logger.LogWarning("OPC item {Item} rejected: {Error}", item, error);
        }
    }

    public ControllerState Read()
    {
        if (_client is null)
        {
            return Disconnected();
        }

        IReadOnlyDictionary<string, OpcValue> v;
        try
        {
            v = _client.Read();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _logger.LogWarning(ex, "OPC read for furnace {Furnace} failed", _options.Code);
            _client.Dispose();
            _client = null;
            return Disconnected();
        }

        var t = _options.Tags;
        return new ControllerState(
            DateTime.UtcNow,
            true,
            Bool(v, Tag(t.CycleActive)),
            Text(v, Tag(t.RecipeNumber)),
            Number(v, Tag(t.Setpoint)),
            Number(v, Tag(t.ControlTemperature)),
            Bool(v, Tag(t.DoorOpen)),
            Bool(v, Tag(t.QuenchDown)),
            (int)(Number(v, Tag(t.AlarmWord)) ?? 0),
            Bool(v, Tag(t.OverTemperature)));
    }

    private static ControllerState Disconnected() =>
        new(DateTime.UtcNow, false, false, null, null, null, false, false, 0, false);

    private static bool Bool(IReadOnlyDictionary<string, OpcValue> v, string tag) =>
        v.TryGetValue(tag, out var x) && x.IsGood && Convert.ToBoolean(x.Value, CultureInfo.InvariantCulture);

    private static double? Number(IReadOnlyDictionary<string, OpcValue> v, string tag) =>
        v.TryGetValue(tag, out var x) && x.IsGood && x.Value is not null ? Convert.ToDouble(x.Value, CultureInfo.InvariantCulture) : null;

    private static string? Text(IReadOnlyDictionary<string, OpcValue> v, string tag) =>
        v.TryGetValue(tag, out var x) && x.IsGood ? Convert.ToString(x.Value, CultureInfo.InvariantCulture) : null;

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
