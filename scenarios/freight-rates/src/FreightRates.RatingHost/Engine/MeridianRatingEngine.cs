using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using FreightRates.Contracts;
using FreightRates.Core;
using FreightRates.RatingHost.Interop;
using Microsoft.Extensions.Options;

namespace FreightRates.RatingHost.Engine;

public sealed class MeridianRatingEngine : IHostedRatingEngine, IHostedService, IDisposable
{
    private static int _resolverSet;

    private readonly EngineOptions _options;
    private readonly ILogger<MeridianRatingEngine> _logger;
    private readonly EngineThread _thread = new("MRS RateEngine");
    private int _session;
    private string _engineVersion = "";

    public MeridianRatingEngine(IOptions<EngineOptions> options, ILogger<MeridianRatingEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsOpen => _session != 0;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Environment.Is64BitProcess)
            throw new InvalidOperationException("MRSRATE32.DLL is a 32-bit library; publish the rating host for win-x86.");

        RegisterResolver(_options.SdkPath);
        try
        {
            await _thread.InvokeAsync(Open, cancellationToken);
        }
        catch (RatingException ex)
        {
            // The host keeps running so the health endpoint reports the problem;
            // the session is opened again on the next rating call.
            _logger.LogError(ex, "Could not open RateEngine session: {Message} ({Code})", ex.Message, ex.Code);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _thread.InvokeAsync(Close, CancellationToken.None);

    public Task<EngineRateResult> RateAsync(ShipmentRequest shipment, CancellationToken cancellationToken = default) =>
        CallAsync(() => Rate(shipment), cancellationToken);

    public Task<TariffInfo> GetTariffInfoAsync(CancellationToken cancellationToken = default) =>
        CallAsync(GetTariffInfo, cancellationToken);

    public Task<TariffInfo> ReloadTariffsAsync(CancellationToken cancellationToken = default) =>
        CallAsync(() =>
        {
            Check(NativeMethods.MR_ReloadTariffs(_session));
            _logger.LogInformation("Tariff data reloaded from {DataPath}", _options.DataPath);
            return GetTariffInfo();
        }, cancellationToken);

    public Task<KeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default) =>
        _thread.InvokeAsync(ReadKeyStatus, cancellationToken);

    private async Task<T> CallAsync<T>(Func<T> call, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.CallTimeoutSeconds));
        return await _thread.InvokeAsync(() =>
        {
            if (_session == 0)
                Open();
            try
            {
                return call();
            }
            catch (RatingException ex) when (EngineErrors.RequiresReinitialize(ex.Code))
            {
                _logger.LogWarning("RateEngine session lost ({Code}: {Message}); opening a new session", ex.Code, ex.Message);
                Close();
                Open();
                return call();
            }
        }, timeout.Token);
    }

    private void Open()
    {
        var version = new StringBuilder(32);
        NativeMethods.MR_GetVersion(version, version.Capacity);
        _engineVersion = version.ToString();

        var key = ReadKeyStatus();
        if (!key.Present)
            throw new RatingException(RatingErrorKind.EngineUnavailable, EngineErrors.MR_E_NOKEY, EngineErrors.Describe(EngineErrors.MR_E_NOKEY));

        var password = LicencePassword.Unprotect(_options.EncryptedPassword);
        var rc = NativeMethods.MR_Initialize(_options.DataPath, _options.LicenceFile, password, out var session);
        if (rc != EngineErrors.MR_OK)
            throw new RatingException(EngineErrors.Classify(rc), rc, EngineErrors.Describe(rc));

        _session = session;
        _logger.LogInformation(
            "RateEngine {Version} session opened, key {Serial}, licence days left {Days}, seats {InUse}/{Seats}",
            _engineVersion, key.Serial, key.LicenceDaysLeft, key.SeatsInUse, key.Seats);
        if (key.LicenceDaysLeft <= 30)
            _logger.LogWarning("RateEngine licence expires in {Days} days", key.LicenceDaysLeft);
    }

    private void Close()
    {
        if (_session == 0)
            return;
        NativeMethods.MR_Terminate(_session);
        _session = 0;
    }

    private EngineRateResult Rate(ShipmentRequest shipment)
    {
        var header = new MR_SHIPMENT_HEADER
        {
            OriginZip = shipment.OriginZip,
            DestZip = shipment.DestinationZip,
            OriginCountry = _options.Country,
            DestCountry = _options.Country,
            ShipDate = RatingMapping.ToDateInt(shipment.ShipDate),
            TariffName = _options.DefaultTariff,
            Flags = NativeMethods.MR_FLAG_SINGLE_SHIPMENT | NativeMethods.MR_FLAG_TRANSIT_DAYS
                    | (_options.DeficitRating ? NativeMethods.MR_FLAG_DEFICIT_RATING : 0),
        };

        var lines = shipment.Lines
            .Select(l => new MR_LINE_ITEM { ClassX10 = RatingMapping.ClassToX10(FreightClass.Resolve(l)), WeightLbs = l.WeightLbs })
            .ToArray();

        var result = MR_RATE_RESULT.Create();
        Check(NativeMethods.MR_RateShipment(_session, ref header, lines, lines.Length, ref result));
        Check(result.Status);

        return new EngineRateResult
        {
            GrossCharge = result.TotalCents / 100m,
            TariffMinimumCharge = result.MinimumChargeCents / 100m,
            DeficitWeightLbs = result.DeficitWeightLbs,
            DeficitCharge = result.DeficitChargeCents / 100m,
            TransitDays = result.TransitDays,
            TariffName = result.TariffUsed.Trim(),
            OriginTerminal = result.OriginTerminal.Trim(),
            DestinationTerminal = result.DestTerminal.Trim(),
            Lines = lines
                .Select((l, i) => new EngineLineRate(
                    RatingMapping.ClassFromX10(l.ClassX10),
                    l.WeightLbs,
                    result.Lines[i].RatePerCwtCents / 100m,
                    result.Lines[i].ChargeCents / 100m))
                .ToList(),
        };
    }

    private TariffInfo GetTariffInfo()
    {
        var name = new StringBuilder(16);
        Check(NativeMethods.MR_GetTariffInfo(_session, name, name.Capacity, out var effective));
        var key = ReadKeyStatus();
        return new TariffInfo(name.ToString(), RatingMapping.FromDateInt(effective), _engineVersion, key.Serial, key.LicenceDaysLeft);
    }

    private static KeyStatus ReadKeyStatus()
    {
        var status = new MR_KEY_STATUS { Serial = "" };
        var rc = NativeMethods.MR_GetKeyStatus(ref status);
        if (rc != EngineErrors.MR_OK)
            return new KeyStatus(false, "", 0, 0, 0);
        return new KeyStatus(status.Present != 0, status.Serial.Trim(), status.LicenceDaysLeft, status.Seats, status.SeatsInUse);
    }

    private void Check(int rc)
    {
        if (rc == EngineErrors.MR_OK)
            return;

        var message = EngineErrors.Describe(rc);
        if (_session != 0)
        {
            var text = new StringBuilder(256);
            if (NativeMethods.MR_GetLastErrorText(_session, text, text.Capacity) == EngineErrors.MR_OK && text.Length > 0)
                message = $"{message}: {text}";
        }
        throw new RatingException(EngineErrors.Classify(rc), rc, message);
    }

    private static void RegisterResolver(string sdkPath)
    {
        if (Interlocked.Exchange(ref _resolverSet, 1) == 1)
            return;

        var binPath = Path.Combine(sdkPath, "bin");
        // MRSRATE32.DLL loads MRSKEY32.DLL from the DLL search path.
        NativeMethods.SetDllDirectory(binPath);
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, assembly, searchPath) =>
            string.Equals(name, NativeMethods.EngineDll, StringComparison.OrdinalIgnoreCase)
                ? NativeLibrary.Load(Path.Combine(binPath, NativeMethods.EngineDll))
                : IntPtr.Zero);
    }

    public void Dispose() => _thread.Dispose();
}

