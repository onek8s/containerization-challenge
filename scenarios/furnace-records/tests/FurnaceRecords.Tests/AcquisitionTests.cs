using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FurnaceRecords.Acquisition;
using FurnaceRecords.Acquisition.Board;
using FurnaceRecords.Acquisition.Opc;
using FurnaceRecords.Acquisition.Simulation;
using FurnaceRecords.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace FurnaceRecords.Tests;

public class SimulatorTests
{
    private static readonly SimulatorOptions Options = new() { TimeScale = 60, HeatingCelsiusPerMinute = 12, QuenchDelaySeconds = 7 };

    [Fact]
    public void Board_reports_ambient_emf_relative_to_cold_junction()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero));
        var furnace = new SimulatedPlant(Options, time).Get("F01");
        var board = new SimulatedBoard(furnace, time);
        board.Open();
        board.Configure(new[] { new ChannelSetup(0, ThermocoupleType.K), new ChannelSetup(4, ThermocoupleType.N) }, 10);
        board.Start();
        time.Advance(TimeSpan.FromSeconds(1));

        var frames = board.ReadFrames(TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(60, frames.Count);
        var last = frames[^1];
        Assert.Equal(new[] { 0, 4 }, last.Microvolts.Keys.OrderBy(k => k));
        var celsius = Thermocouple.Compensate(ThermocoupleType.N, last.Microvolts[4], last.ColdJunctionCelsius);
        Assert.Equal(22, celsius, 0);
    }

    [Fact]
    public void Cycle_heats_soaks_opens_door_and_quenches()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero));
        var furnace = new SimulatedPlant(Options, time).Get("F01");
        var controller = new SimulatedController(furnace);
        furnace.StartCycle(new SimulatedCycle("ST-7075", 480, 50, Quench: true));

        var states = new List<ControllerState>();
        for (var i = 0; i < 240 * 60; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1.0 / 60));
            states.Add(controller.Read());
            if (!states[^1].CycleActive) break;
        }

        Assert.False(states[^1].CycleActive);
        var door = states.First(s => s.DoorOpen);
        var quench = states.First(s => s.QuenchDown);
        Assert.InRange((quench.AtUtc - door.AtUtc).TotalSeconds, 6, 8);
        Assert.InRange(states.Max(s => s.ControlCelsius!.Value), 480, 482);
        Assert.Throws<InvalidOperationException>(() => { furnace.StartCycle(new SimulatedCycle("X", 100, 1, false)); furnace.StartCycle(new SimulatedCycle("X", 100, 1, false)); });
    }

    [Fact]
    public void Factory_returns_simulated_sources()
    {
        var options = new AcquisitionOptions { Provider = "Simulator" };
        var factory = new AcquisitionFactory(options, new SimulatedPlant(options.Simulator, TimeProvider.System), TimeProvider.System, NullLoggerFactory.Instance);
        var f = new FurnaceAcquisitionOptions { Code = "F09" };
        Assert.IsType<SimulatedBoard>(factory.CreateBoard(f));
        Assert.IsType<SimulatedController>(factory.CreateController(f));
    }
}

public class BoardInterfaceTests
{
    private static string Idl => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vendor", "TcxBoard.idl"));

    [Fact]
    public void Dispatch_ids_match_type_library()
    {
        var idl = Idl;
        var expected = Regex.Matches(idl, @"\bid\((\d+)\)\]\s+HRESULT\s+(\w+)")
            .ToDictionary(m => m.Groups[2].Value, m => int.Parse(m.Groups[1].Value));
        var members = typeof(ITcxBoard).GetMembers()
            .Select(m => (m.Name, Id: m.GetCustomAttribute<DispIdAttribute>()?.Value))
            .Where(m => m.Id is not null)
            .ToDictionary(m => m.Name, m => m.Id!.Value);

        Assert.Equal(expected.Count, members.Count);
        foreach (var (name, id) in expected)
        {
            Assert.True(members.TryGetValue(name, out var actual), $"{name} missing");
            Assert.Equal(id, actual);
        }
    }

    [Fact]
    public void Guids_match_type_library()
    {
        var idl = Idl;
        Assert.Contains(typeof(ITcxBoard).GUID.ToString().ToUpperInvariant(), idl);
        Assert.Contains(TcxBoardLib.BoardClsid.ToString().ToUpperInvariant(), idl);
        Assert.Contains(TcxBoardLib.ProgId, idl);
    }

    [Fact]
    public void Opc_structures_have_native_layout()
    {
        Assert.Equal(IntPtr.Size == 8 ? 48 : 28, Marshal.SizeOf<OpcItemDef>());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 20, Marshal.SizeOf<OpcItemResult>());
        Assert.Equal(16, Marshal.OffsetOf<OpcItemState>(nameof(OpcItemState.Value)).ToInt32());
    }

    [Fact]
    public void Opc_quality_good()
    {
        Assert.True(new OpcValue(1, 0xC0, DateTime.UtcNow).IsGood);
        Assert.True(new OpcValue(1, 0xD8, DateTime.UtcNow).IsGood);
        Assert.False(new OpcValue(1, 0x40, DateTime.UtcNow).IsGood);
        Assert.False(new OpcValue(1, 0x00, DateTime.UtcNow).IsGood);
    }
}
