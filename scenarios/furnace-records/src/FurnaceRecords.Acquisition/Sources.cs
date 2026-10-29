using FurnaceRecords.Core;

namespace FurnaceRecords.Acquisition;

public sealed record BoardInfo(
    string Model,
    uint Serial,
    string Firmware,
    string Driver,
    int ChannelCount,
    int MaxSampleRateHz,
    DateOnly? CalibrationDate);

public sealed record ChannelSetup(int Channel, ThermocoupleType Type);

/// <summary>One scan of all enabled channels.</summary>
public sealed record RawFrame(
    uint Sequence,
    DateTime AtUtc,
    uint Status,
    uint OpenCircuitMask,
    double ColdJunctionCelsius,
    IReadOnlyDictionary<int, int> Microvolts)
{
    public bool IsOpen(int channel) => (OpenCircuitMask & (1u << channel)) != 0;
}

/// <summary>Thermocouple input board of one furnace.</summary>
public interface IThermocoupleSource : IDisposable
{
    BoardInfo Open();

    void Configure(IReadOnlyList<ChannelSetup> channels, int sampleRateHz);

    void Start();

    /// <summary>Waits up to <paramref name="timeout"/> for new frames and returns them in order.</summary>
    IReadOnlyList<RawFrame> ReadFrames(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Frames lost because they were not read in time.</summary>
    long LostFrames { get; }

    void Stop();
}

/// <summary>State of the furnace controller (PLC) as published by the HMI.</summary>
public sealed record ControllerState(
    DateTime AtUtc,
    bool Connected,
    bool CycleActive,
    string? RecipeNumber,
    double? SetpointCelsius,
    double? ControlCelsius,
    bool DoorOpen,
    bool QuenchDown,
    int AlarmWord,
    bool OverTemperatureTripped);

public interface IFurnaceController : IDisposable
{
    void Connect();

    ControllerState Read();
}

public interface IAcquisitionFactory
{
    IThermocoupleSource CreateBoard(FurnaceAcquisitionOptions furnace);

    IFurnaceController CreateController(FurnaceAcquisitionOptions furnace);
}

public sealed class AcquisitionOptions
{
    public const string Section = "Acquisition";

    /// <summary><c>Hardware</c> or <c>Simulator</c>.</summary>
    public string Provider { get; set; } = "Hardware";

    public int SampleRateHz { get; set; } = 10;

    public int RecordIntervalSeconds { get; set; } = 10;

    public int ControllerPollMilliseconds { get; set; } = 500;

    public List<FurnaceAcquisitionOptions> Furnaces { get; set; } = new();

    public SimulatorOptions Simulator { get; set; } = new();
}

public sealed class FurnaceAcquisitionOptions
{
    public string Code { get; set; } = "";

    /// <summary>TCX board number in the acquisition server, 0 for the first board.</summary>
    public int BoardIndex { get; set; }

    /// <summary>Host running the furnace HMI and its OPC server.</summary>
    public string OpcHost { get; set; } = "";

    public string OpcProgId { get; set; } = "Vantor.FurnaceLink.DA.2";

    public string? OpcClsid { get; set; }

    public string TagPrefix { get; set; } = "";

    public OpcTagNames Tags { get; set; } = new();
}

public sealed class OpcTagNames
{
    public string CycleActive { get; set; } = "Cycle.Active";
    public string RecipeNumber { get; set; } = "Cycle.Recipe";
    public string Setpoint { get; set; } = "Zone1.SP";
    public string ControlTemperature { get; set; } = "Zone1.PV";
    public string DoorOpen { get; set; } = "Door.Open";
    public string QuenchDown { get; set; } = "Quench.Down";
    public string AlarmWord { get; set; } = "Alarm.Word";
    public string OverTemperature { get; set; } = "OTC.Tripped";
}

public sealed class SimulatorOptions
{
    /// <summary>Simulated seconds per real second.</summary>
    public double TimeScale { get; set; } = 60;

    /// <summary>Start a cycle for the next planned load of an idle furnace.</summary>
    public bool AutoStartPlannedLoads { get; set; } = true;

    public double AmbientCelsius { get; set; } = 22;

    public double HeatingCelsiusPerMinute { get; set; } = 12;

    public int QuenchDelaySeconds { get; set; } = 7;
}
