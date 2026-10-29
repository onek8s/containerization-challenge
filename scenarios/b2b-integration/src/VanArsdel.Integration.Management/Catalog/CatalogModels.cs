using System.Collections.Generic;

namespace VanArsdel.Integration.Management.Catalog
{
    public class ApplicationSummary
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public List<ReceivePortSummary> ReceivePorts { get; } = new List<ReceivePortSummary>();
        public List<SendPortSummary> SendPorts { get; } = new List<SendPortSummary>();
        public List<OrchestrationSummary> Orchestrations { get; } = new List<OrchestrationSummary>();
    }

    public class ReceivePortSummary
    {
        public string Name { get; set; }
        public bool TwoWay { get; set; }
        public List<ReceiveLocationSummary> Locations { get; } = new List<ReceiveLocationSummary>();
    }

    public class ReceiveLocationSummary
    {
        public string Name { get; set; }
        public string ReceivePort { get; set; }
        public string Transport { get; set; }
        public string Address { get; set; }
        public string Pipeline { get; set; }
        public string Handler { get; set; }
        public bool Enabled { get; set; }
        public bool ServiceWindow { get; set; }
    }

    public class SendPortSummary
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public bool Dynamic { get; set; }
        public bool TwoWay { get; set; }
        public string Transport { get; set; }
        public string Address { get; set; }
        public string Pipeline { get; set; }
        public string Handler { get; set; }
        public int RetryCount { get; set; }
        public int RetryIntervalMinutes { get; set; }
        public bool OrderedDelivery { get; set; }
        public string Filter { get; set; }
    }

    public class OrchestrationSummary
    {
        public string FullName { get; set; }
        public string Status { get; set; }
        public string Host { get; set; }
    }

    public class PartySummary
    {
        public string Name { get; set; }
        public List<PartyAliasSummary> Aliases { get; } = new List<PartyAliasSummary>();
        public int SendPorts { get; set; }
    }

    public class PartyAliasSummary
    {
        public string Name { get; set; }
        public string Qualifier { get; set; }
        public string Value { get; set; }
    }

    public class PartySyncResult
    {
        public string Party { get; set; }
        public bool Created { get; set; }
        public List<string> AddedAliases { get; } = new List<string>();
        public List<string> RemovedAliases { get; } = new List<string>();

        public bool Changed
        {
            get { return Created || AddedAliases.Count > 0 || RemovedAliases.Count > 0; }
        }
    }
}
