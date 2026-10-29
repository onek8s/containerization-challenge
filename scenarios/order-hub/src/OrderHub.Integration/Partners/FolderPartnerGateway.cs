using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Litware.OrderHub.Integration.Partners
{
    /// <summary>
    /// Test transport used when the partner namespace is not reachable (development, test lab):
    /// partner orders are *.json files in the inbound folder, dispatch notices are written to the outbound folder.
    /// </summary>
    public class FolderPartnerGateway : IPartnerGateway
    {
        private readonly string _inbound;
        private readonly string _outbound;
        private readonly TimeSpan _interval;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private Task _loop;

        public FolderPartnerGateway(string inbound, string outbound, TimeSpan interval)
        {
            _inbound = inbound;
            _outbound = outbound;
            _interval = interval;
            Directory.CreateDirectory(_inbound);
            Directory.CreateDirectory(_outbound);
            Directory.CreateDirectory(Path.Combine(_inbound, "done"));
            Directory.CreateDirectory(Path.Combine(_inbound, "deadletter"));
        }

        public void StartReceiving(Func<PartnerMessage, Task> handler)
        {
            _loop = Task.Run(() => LoopAsync(handler));
        }

        private async Task LoopAsync(Func<PartnerMessage, Task> handler)
        {
            while (!_stop.IsCancellationRequested)
            {
                foreach (var file in Directory.GetFiles(_inbound, "*.json"))
                {
                    try
                    {
                        string body = File.ReadAllText(file, Encoding.UTF8);
                        await handler(new PartnerMessage { MessageId = Path.GetFileNameWithoutExtension(file), Body = body });
                        MoveTo(file, "done");
                    }
                    catch (FormatException ex)
                    {
                        Log.Error(ex, "Partner file {File} is not a valid order", file);
                        MoveTo(file, "deadletter");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Partner file {File} failed, will retry", file);
                    }
                }

                try
                {
                    await Task.Delay(_interval, _stop.Token);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        private void MoveTo(string file, string folder)
        {
            string target = Path.Combine(_inbound, folder, Path.GetFileName(file));
            if (File.Exists(target)) File.Delete(target);
            File.Move(file, target);
        }

        public Task SendDispatchNoticeAsync(string messageId, string json, CancellationToken cancellationToken)
        {
            string path = Path.Combine(_outbound, messageId + ".json");
            File.WriteAllText(path + ".tmp", json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _stop.Cancel();
            if (_loop != null) await _loop;
        }

        public void Dispose()
        {
            _stop.Dispose();
        }
    }
}
