using System;
using System.Collections.Generic;
using System.ServiceModel.Activities;
using System.Xml.Linq;
using StaffOnboarding.Contracts;
using StaffOnboarding.Core;

namespace StaffOnboarding.Workflows.Definitions
{
    /// <summary>
    /// Current definitions and the older definitions that persisted instances may still be running.
    /// Remove an old version only when no instance of it is left in the instance store.
    /// </summary>
    public static class WorkflowCatalog
    {
        public static readonly Version Joiner = new Version(4, 2, 0, 0);
        public static readonly Version Mover = new Version(4, 0, 0, 0);
        public static readonly Version Leaver = new Version(4, 1, 0, 0);

        public static IEnumerable<ServiceDefinition> All()
        {
            yield return new ServiceDefinition(
                "joiner",
                ContractNames.Joiner,
                JoinerWorkflow.Create(Joiner, true),
                new[] { JoinerWorkflow.Create(new Version(4, 1, 0, 0), false) });
            yield return new ServiceDefinition("mover", ContractNames.Mover, MoverWorkflow.Create(Mover), new WorkflowService[0]);
            yield return new ServiceDefinition("leaver", ContractNames.Leaver, LeaverWorkflow.Create(Leaver), new WorkflowService[0]);
        }
    }

    public class ServiceDefinition
    {
        public ServiceDefinition(string path, string contract, WorkflowService current, IEnumerable<WorkflowService> supported)
        {
            Path = path;
            ContractName = XName.Get(contract, OnboardingNamespaces.Service);
            Current = current;
            Supported = supported;
        }

        public string Path { get; private set; }
        public XName ContractName { get; private set; }
        public WorkflowService Current { get; private set; }
        public IEnumerable<WorkflowService> Supported { get; private set; }
    }
}
