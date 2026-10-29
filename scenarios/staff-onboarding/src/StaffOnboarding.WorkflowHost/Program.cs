using System;
using System.ServiceProcess;

namespace StaffOnboarding.WorkflowHost
{
    internal static class Program
    {
        /// <summary>
        /// Runs as the "StaffOnboardingWorkflow" Windows service. Operator commands:
        ///   StaffOnboarding.WorkflowHost.exe console
        ///   StaffOnboarding.WorkflowHost.exe instances [status]
        ///   StaffOnboarding.WorkflowHost.exe suspend|unsuspend|terminate|cancel &lt;joiner|mover|leaver&gt; &lt;instanceId&gt; [reason]
        /// </summary>
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                ServiceBase.Run(new OnboardingWorkflowService());
                return 0;
            }

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "console":
                        return RunConsole();
                    case "instances":
                        return InstanceCommands.List(args.Length > 1 ? args[1] : null);
                    case "suspend":
                    case "unsuspend":
                    case "terminate":
                    case "cancel":
                        if (args.Length < 3) return Usage();
                        return InstanceCommands.Control(args[0].ToLowerInvariant(), args[1], Guid.Parse(args[2]),
                            args.Length > 3 ? string.Join(" ", args, 3, args.Length - 3) : "Operator request");
                    default:
                        return Usage();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int RunConsole()
        {
            using (var hosts = new HostSet())
            {
                hosts.Open();
                Console.WriteLine("Staff Onboarding workflow host running. Press Enter to stop.");
                Console.ReadLine();
            }
            return 0;
        }

        private static int Usage()
        {
            Console.Error.WriteLine("Usage: StaffOnboarding.WorkflowHost.exe [console | instances [status] | suspend|unsuspend|terminate|cancel <joiner|mover|leaver> <instanceId> [reason]]");
            return 2;
        }
    }
}
