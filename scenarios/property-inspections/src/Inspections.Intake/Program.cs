using System;
using System.ServiceProcess;
using System.Threading;

namespace Northwind.Inspections.Intake
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (Environment.UserInteractive || (args.Length > 0 && args[0] == "--console"))
            {
                var service = new IntakeService();
                Console.WriteLine("Inspections intake running in console mode. Press Enter to stop.");
                service.StartInteractive();
                using (var stop = new ManualResetEvent(false))
                {
                    Console.CancelKeyPress += (s, e) => { e.Cancel = true; stop.Set(); };
                    ThreadPool.QueueUserWorkItem(_ => { Console.ReadLine(); stop.Set(); });
                    stop.WaitOne();
                }
                service.StopInteractive();
                return 0;
            }

            ServiceBase.Run(new IntakeService());
            return 0;
        }
    }
}
