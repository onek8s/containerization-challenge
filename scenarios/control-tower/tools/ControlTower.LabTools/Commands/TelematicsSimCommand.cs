using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>
/// Simulates the truck telematics units: every vehicle on an open road leg reports positions along the
/// straight line between the leg's end points, at the planned pace (optionally sped up). Reefer units
/// report their temperature; now and then one drifts out of range for a while.
/// </summary>
public sealed class TelematicsSimCommand : ILabCommand
{
    public string Name => "telematics-sim";

    public string Usage => "--connection <db connection string> [--provider SqlServer|Sqlite] [--broker localhost] [--port 1883] [--user u --password p] " +
                           "[--tls] [--interval 10] [--speedup 1] [--excursion-percent 2] [--topic proseware/telematics]";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var connection = a.Required("connection");
        var provider = a.Get("provider") ?? LabDatabase.GuessProvider(connection);
        var interval = TimeSpan.FromSeconds(a.GetInt("interval", 10));
        var speedup = a.GetInt("speedup", 1);
        var excursionPercent = a.GetInt("excursion-percent", 2);
        var topicRoot = a.Get("topic", "proseware/telematics").TrimEnd('/');
        var random = new Random();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId("ct-telematics-sim-" + Environment.MachineName.ToLowerInvariant())
            .WithTcpServer(a.Get("broker", "localhost"), a.GetInt("port", 1883))
            .WithCleanSession();
        if (a.Get("user") is { } user) optionsBuilder.WithCredentials(user, a.Get("password"));
        if (a.Has("tls")) optionsBuilder.WithTls();

        using var client = new MqttFactory().CreateMqttClient();
        await client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        Console.WriteLine("Connected to the broker. Ctrl+C stops.");

        var simStart = DateTimeOffset.UtcNow;
        var excursions = new Dictionary<int, DateTimeOffset>();
        var odometer = new Dictionary<int, double>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var simNow = simStart + (DateTimeOffset.UtcNow - simStart) * speedup;
            await using (var db = LabDatabase.Open(connection, provider))
            {
                var legs = await db.Legs.AsNoTracking()
                    .Include(l => l.From).Include(l => l.To).Include(l => l.Vehicle)
                    .Where(l => l.Mode == TransportMode.Road && l.VehicleId != null
                                && (l.Status == LegStatus.Departed || l.Status == LegStatus.Confirmed))
                    .ToListAsync(cancellationToken);

                // one leg per vehicle: the one that is (or should be) running now
                var running = legs
                    .Where(l => l.Vehicle!.TelematicsUnitId != null && l.PlannedDeparture <= simNow.AddMinutes(30))
                    .GroupBy(l => l.VehicleId)
                    .Select(g => g.OrderByDescending(l => l.Status == LegStatus.Departed).ThenBy(l => l.PlannedDeparture).First())
                    .ToList();

                var shipments = await db.Shipments.AsNoTracking()
                    .Where(s => running.Select(l => l.ShipmentId).Contains(s.Id))
                    .Select(s => new { s.Id, s.TemperatureMin, s.TemperatureMax })
                    .ToDictionaryAsync(s => s.Id, cancellationToken);

                var sent = 0;
                foreach (var leg in running)
                {
                    var vehicle = leg.Vehicle!;
                    var total = (leg.PlannedArrival - leg.PlannedDeparture).TotalSeconds;
                    var progress = total <= 0 ? 1 : Math.Clamp((simNow - leg.PlannedDeparture).TotalSeconds / total, 0, 1);
                    // stand still at the start until departure and at the end on arrival
                    var lat = leg.From!.Position.Y + (leg.To!.Position.Y - leg.From.Position.Y) * progress + Jitter(random);
                    var lon = leg.From.Position.X + (leg.To.Position.X - leg.From.Position.X) * progress + Jitter(random);
                    var moving = progress is > 0 and < 1;
                    var speed = moving ? 60 + random.Next(30) : 0;
                    var heading = (int)Domain.Locations.GeoPoints.BearingDegrees(leg.From.Position, leg.To.Position);
                    odometer[vehicle.Id] = odometer.GetValueOrDefault(vehicle.Id, 250_000 + vehicle.Id * 1_000) + speed * interval.TotalHours * speedup;

                    await PublishAsync(client, $"{topicRoot}/{vehicle.TelematicsUnitId}/position",
                        $"{{\"unit\":\"{vehicle.TelematicsUnitId}\",\"ts\":\"{simNow:yyyy-MM-ddTHH:mm:ssZ}\",\"lat\":{F(lat, 6)},\"lon\":{F(lon, 6)}," +
                        $"\"spd\":{speed},\"hdg\":{heading},\"ign\":{(moving ? "true" : "false")},\"odo\":{(long)odometer[vehicle.Id]},\"sat\":{8 + random.Next(5)}}}",
                        cancellationToken);
                    sent++;

                    if (vehicle.HasReefer && shipments.TryGetValue(leg.ShipmentId, out var s) && (s.TemperatureMin is not null || s.TemperatureMax is not null))
                    {
                        var setpoint = ((s.TemperatureMin ?? s.TemperatureMax ?? 5) + (s.TemperatureMax ?? s.TemperatureMin ?? 5)) / 2;
                        if (!excursions.ContainsKey(vehicle.Id) && random.Next(100) < excursionPercent)
                            excursions[vehicle.Id] = DateTimeOffset.UtcNow.AddMinutes(10);
                        var inExcursion = excursions.TryGetValue(vehicle.Id, out var until) && until > DateTimeOffset.UtcNow;
                        if (!inExcursion) excursions.Remove(vehicle.Id);
                        var temperature = inExcursion ? (s.TemperatureMax ?? setpoint) + 3.5m : setpoint + (decimal)(random.NextDouble() * 1.6 - 0.8);
                        await PublishAsync(client, $"{topicRoot}/{vehicle.TelematicsUnitId}/reefer",
                            $"{{\"unit\":\"{vehicle.TelematicsUnitId}\",\"ts\":\"{simNow:yyyy-MM-ddTHH:mm:ssZ}\",\"t1\":{F((double)temperature, 1)},\"t2\":{F((double)temperature + 0.6, 1)}," +
                            $"\"sp\":{F((double)setpoint, 1)},\"door\":{(!moving && random.Next(10) == 0 ? "true" : "false")},\"mode\":\"CONT\"}}",
                            cancellationToken);
                    }
                }
                Console.WriteLine($"{simNow:HH:mm:ss} sim time: {sent} positions, {excursions.Count} reefer excursions");
            }
            await Task.Delay(interval, cancellationToken);
        }
        await client.DisconnectAsync();
        return 0;
    }

    private static Task PublishAsync(IMqttClient client, string topic, string json, CancellationToken ct) =>
        client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(json))
            .WithAtLeastOnceQoS()
            .Build(), ct);

    private static double Jitter(Random r) => (r.NextDouble() - 0.5) * 0.0004;

    private static string F(double v, int decimals) => Math.Round(v, decimals).ToString(CultureInfo.InvariantCulture);
}
