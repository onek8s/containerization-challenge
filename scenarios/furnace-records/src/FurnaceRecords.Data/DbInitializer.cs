using FurnaceRecords.Core;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Data;

public static class DbInitializer
{
    /// <summary>API key of the sample MES client.</summary>
    public const string SampleApiKey = "fr_dev_mes01_7d3e91a4";

    public static async Task InitializeAsync(FurnaceRecordsContext db, DatabaseOptions options, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer())
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
        if (!options.SeedSampleData || await db.Furnaces.AnyAsync(ct))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Furnaces.AddRange(
            Furnace("F01", "Solution treatment furnace 1", 2, InstrumentationType.B, today, ThermocoupleType.N,
                ("Control", ChannelRole.Control), ("Over-temperature", ChannelRole.OverTemperature),
                ("Recorder hot", ChannelRole.Recorder), ("Recorder cold", ChannelRole.Recorder),
                ("Load 1", ChannelRole.Load), ("Load 2", ChannelRole.Load), ("Load 3", ChannelRole.Load), ("Load 4", ChannelRole.Load),
                ("Survey front left", ChannelRole.Survey), ("Survey front right", ChannelRole.Survey), ("Survey rear left", ChannelRole.Survey),
                ("Survey rear right", ChannelRole.Survey), ("Survey centre", ChannelRole.Survey), ("Survey top", ChannelRole.Survey),
                ("Survey bottom", ChannelRole.Survey), ("Survey door", ChannelRole.Survey), ("Survey back wall", ChannelRole.Survey)),
            Furnace("F02", "Ageing oven 2", 3, InstrumentationType.C, today, ThermocoupleType.K,
                ("Control", ChannelRole.Control), ("Over-temperature", ChannelRole.OverTemperature),
                ("Recorder", ChannelRole.Recorder), ("Load 1", ChannelRole.Load), ("Load 2", ChannelRole.Load)));

        db.Recipes.AddRange(
            new Recipe { Code = "ST-7075", Name = "Solution treat 7075", Specification = "AMS 2770", SetpointCelsius = 480, ToleranceMinusCelsius = 6, TolerancePlusCelsius = 6, SoakMinutes = 50, MaxSoakMinutes = 120, MaxRampCelsiusPerMinute = 15, MaxQuenchDelaySeconds = 15 },
            new Recipe { Code = "ST-2024", Name = "Solution treat 2024", Specification = "AMS 2770", SetpointCelsius = 493, ToleranceMinusCelsius = 6, TolerancePlusCelsius = 6, SoakMinutes = 40, MaxSoakMinutes = 90, MaxRampCelsiusPerMinute = 15, MaxQuenchDelaySeconds = 15 },
            new Recipe { Code = "AG-T6", Name = "Age T6", Specification = "AMS 2770", SetpointCelsius = 121, ToleranceMinusCelsius = 8, TolerancePlusCelsius = 8, SoakMinutes = 60 },
            new Recipe { Code = "TUS-480", Name = "Uniformity survey 480 °C", Specification = "AMS 2750", SetpointCelsius = 480, ToleranceMinusCelsius = 6, TolerancePlusCelsius = 6, SoakMinutes = 35 },
            new Recipe { Code = "TUS-177", Name = "Uniformity survey 177 °C", Specification = "AMS 2750", SetpointCelsius = 177, ToleranceMinusCelsius = 8, TolerancePlusCelsius = 8, SoakMinutes = 35 },
            new Recipe { Code = "AG-T73", Name = "Age T73 second step", Specification = "AMS 2770", SetpointCelsius = 177, ToleranceMinusCelsius = 8, TolerancePlusCelsius = 8, SoakMinutes = 45, MaxRampCelsiusPerMinute = 10 });

        db.ApiClients.Add(new ApiClient { Name = "MES01", KeyHash = ApiKeyService.Hash(SampleApiKey), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    private static Furnace Furnace(string code, string name, int cls, InstrumentationType type, DateOnly today, ThermocoupleType tc, params (string Name, ChannelRole Role)[] channels) => new()
    {
        Code = code,
        Name = name,
        FurnaceClass = cls,
        Instrumentation = type,
        UniformitySurveyDue = today.AddMonths(3),
        AccuracyTestDue = today.AddMonths(1),
        Channels = channels.Select((c, i) => new FurnaceChannel
        {
            Channel = i,
            Name = c.Name,
            Role = c.Role,
            Type = tc,
            SensorSerial = $"{code}-TC{i:00}",
            CalibratedOn = today.AddMonths(-2),
            CalibrationDue = today.AddMonths(c.Role == ChannelRole.Load ? 1 : 10),
            Corrections = new List<ChannelCorrection>
            {
                new() { AtCelsius = 120, CorrectionCelsius = Math.Round(0.1 * (i % 3) - 0.1, 1) },
                new() { AtCelsius = 480, CorrectionCelsius = Math.Round(0.2 * (i % 4) - 0.3, 1) },
            },
        }).ToList(),
    };
}
