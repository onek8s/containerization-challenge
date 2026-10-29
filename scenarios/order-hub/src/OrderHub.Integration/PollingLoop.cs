using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Litware.OrderHub.Integration
{
    /// <summary>Runs an action at a fixed interval until stopped; failures are logged and the next round runs as usual.</summary>
    public class PollingLoop
    {
        private readonly string _name;
        private readonly TimeSpan _interval;
        private readonly Func<Task> _action;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private Task _task;

        public PollingLoop(string name, TimeSpan interval, Func<Task> action)
        {
            _name = name;
            _interval = interval;
            _action = action;
        }

        public void Start()
        {
            _task = Task.Run(RunAsync);
            Log.Information("{Loop} started, interval {Interval}", _name, _interval);
        }

        private async Task RunAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await _action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{Loop} failed", _name);
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

        public void Stop()
        {
            _stop.Cancel();
            try
            {
                _task?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
            }
        }
    }
}
