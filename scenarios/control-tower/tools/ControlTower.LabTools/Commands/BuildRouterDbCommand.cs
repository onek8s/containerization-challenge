using Itinero;
using Itinero.IO.Osm;
using Itinero.Osm.Vehicles;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>Builds the Itinero router database used for road ETAs from an OpenStreetMap extract (.osm.pbf).</summary>
public sealed class BuildRouterDbCommand : ILabCommand
{
    public string Name => "build-routerdb";

    public string Usage => "--input <extract.osm.pbf> --output <file.routerdb> [--no-contract]";

    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var input = a.Required("input");
        var output = a.Required("output");
        if (!File.Exists(input)) throw new LabArgumentException($"{input} not found.");

        var started = DateTime.UtcNow;
        var db = new RouterDb();
        using (var stream = File.OpenRead(input))
        {
            Console.WriteLine($"Loading {input} ({new FileInfo(input).Length / 1024 / 1024} MB) for the car profile...");
            db.LoadOsmData(stream, Vehicle.Car);
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (!a.Has("no-contract"))
        {
            Console.WriteLine("Contracting the network for the fastest car profile...");
            db.AddContracted(Vehicle.Car.Fastest());
        }

        var tmp = output + ".tmp";
        using (var stream = File.Create(tmp))
        {
            db.Serialize(stream);
        }
        File.Move(tmp, output, overwrite: true);
        Console.WriteLine($"Wrote {output} ({new FileInfo(output).Length / 1024 / 1024} MB) in {(DateTime.UtcNow - started).TotalMinutes:0.0} min.");
        return Task.FromResult(0);
    }
}
