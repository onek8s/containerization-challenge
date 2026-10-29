using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>
/// Stands in for a Zebra label printer: accepts raw print jobs on TCP 9100 and saves each job
/// as a .zpl file. Answers the ~HS host status query like a ready printer.
/// </summary>
public sealed class PrinterSinkCommand : ILabCommand
{
    public string Name => "printer-sink";

    public string Usage => "[--port 9100] [--folder .\\printed] [--name zt-vantaa-1]";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var port = a.GetInt("port", 9100);
        var folder = Path.GetFullPath(a.Get("folder", "printed"));
        var name = a.Get("name", "printer");
        Directory.CreateDirectory(folder);

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Printer sink '{name}' listening on port {port}, jobs saved to {folder}. Ctrl+C stops.");
        using var registration = cancellationToken.Register(listener.Stop);
        var job = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                var number = Interlocked.Increment(ref job);
                _ = Task.Run(() => HandleAsync(client, folder, name, number, cancellationToken), cancellationToken);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
        return 0;
    }

    private static async Task HandleAsync(TcpClient client, string folder, string name, int number, CancellationToken ct)
    {
        using (client)
        {
            var remote = client.Client.RemoteEndPoint;
            var stream = client.GetStream();
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            client.ReceiveTimeout = 5000;
            try
            {
                while (true)
                {
                    using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    idle.CancelAfter(TimeSpan.FromSeconds(3));
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(chunk, idle.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        break; // sender keeps the connection open after the job
                    }
                    if (read == 0) break;
                    buffer.Write(chunk, 0, read);

                    var text = Encoding.ASCII.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
                    if (text.TrimEnd().EndsWith("~HS", StringComparison.Ordinal))
                    {
                        // Three status strings of a ready, idle printer.
                        var status = "\u0002030,0,0,1245,000,0,0,0,000,0,0,0\u0003\r\n\u0002000,0,0,0,0,2,4,0,00000000,1,000\u0003\r\n\u00021234,0\u0003\r\n";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(status), ct);
                        buffer.SetLength(0);
                    }
                }
            }
            catch (IOException)
            {
            }

            if (buffer.Length == 0) return;
            var file = Path.Combine(folder, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}_{number:D4}.zpl");
            await File.WriteAllBytesAsync(file, buffer.ToArray(), ct);
            var labels = Encoding.ASCII.GetString(buffer.ToArray()).Split("^XZ").Length - 1;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} job {number} from {remote}: {buffer.Length} bytes, {labels} label(s) -> {Path.GetFileName(file)}");
        }
    }
}
