using FurnaceRecords.Core;

namespace FurnaceRecords.Data.Entities;

public class Furnace
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int FurnaceClass { get; set; }
    public InstrumentationType Instrumentation { get; set; }
    public bool Active { get; set; } = true;
    public DateOnly? UniformitySurveyDue { get; set; }
    public DateOnly? AccuracyTestDue { get; set; }
    public List<FurnaceChannel> Channels { get; set; } = new();
}

public class FurnaceChannel
{
    public int Id { get; set; }
    public int FurnaceId { get; set; }
    public Furnace? Furnace { get; set; }
    public int Channel { get; set; }
    public string Name { get; set; } = "";
    public ChannelRole Role { get; set; }
    public ThermocoupleType Type { get; set; }
    public string? SensorSerial { get; set; }
    public DateOnly? CalibratedOn { get; set; }
    public DateOnly? CalibrationDue { get; set; }
    public bool Enabled { get; set; } = true;
    public List<ChannelCorrection> Corrections { get; set; } = new();
}

public class ChannelCorrection
{
    public int Id { get; set; }
    public int FurnaceChannelId { get; set; }
    public double AtCelsius { get; set; }
    public double CorrectionCelsius { get; set; }
}

public class Recipe
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Specification { get; set; }
    public double SetpointCelsius { get; set; }
    public double ToleranceMinusCelsius { get; set; }
    public double TolerancePlusCelsius { get; set; }
    public int SoakMinutes { get; set; }
    public int? MaxSoakMinutes { get; set; }
    public double? MaxRampCelsiusPerMinute { get; set; }
    public int? MaxQuenchDelaySeconds { get; set; }
    public bool Active { get; set; } = true;

    public RecipeSpec ToSpec() => new()
    {
        Code = Code,
        Name = Name,
        Specification = Specification,
        SetpointCelsius = SetpointCelsius,
        ToleranceMinusCelsius = ToleranceMinusCelsius,
        TolerancePlusCelsius = TolerancePlusCelsius,
        SoakMinutes = SoakMinutes,
        MaxSoakMinutes = MaxSoakMinutes,
        MaxRampCelsiusPerMinute = MaxRampCelsiusPerMinute,
        MaxQuenchDelaySeconds = MaxQuenchDelaySeconds,
    };
}

public enum LoadStatus
{
    Planned,
    Running,
    Completed,
    Released,
    Rejected,
}

public enum LoadKind
{
    Production,
    Survey,
}

public class Load
{
    public int Id { get; set; }
    public string LoadNumber { get; set; } = "";
    public LoadKind Kind { get; set; }
    public int FurnaceId { get; set; }
    public Furnace? Furnace { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public LoadStatus Status { get; set; }
    public DateTime PlannedAtUtc { get; set; }
    public string? PlannedBy { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public bool? Passed { get; set; }
    public DateTime? SoakStartUtc { get; set; }
    public DateTime? SoakEndUtc { get; set; }
    public double? SoakMinutes { get; set; }
    public double? MaxRampCelsiusPerMinute { get; set; }
    public double? QuenchDelaySeconds { get; set; }
    public string? FindingsJson { get; set; }
    public double? UniformityCelsius { get; set; }
    public string? SurveyJson { get; set; }
    public string? CertificatePath { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<LoadPart> Parts { get; set; } = new();
}

public class LoadPart
{
    public int Id { get; set; }
    public int LoadId { get; set; }
    public string WorkOrder { get; set; } = "";
    public string PartNumber { get; set; } = "";
    public string? Lot { get; set; }
    public int Quantity { get; set; }
}

public class LoadSample
{
    public long Id { get; set; }
    public int LoadId { get; set; }
    public DateTime AtUtc { get; set; }
    public int Channel { get; set; }
    public double Celsius { get; set; }
}

public class LoadEventRecord
{
    public long Id { get; set; }
    public int LoadId { get; set; }
    public DateTime AtUtc { get; set; }
    public LoadEventKind Kind { get; set; }
    public string? Detail { get; set; }
}

public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public class OutboxMessage
{
    public long Id { get; set; }
    public string Topic { get; set; } = "";
    public string Key { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}

public class JobRun
{
    public string Job { get; set; } = "";
    public DateTime LastRunUtc { get; set; }
    public string? Result { get; set; }
}
