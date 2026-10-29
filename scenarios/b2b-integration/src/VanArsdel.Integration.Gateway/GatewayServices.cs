using System;
using System.IO;
using System.Web.Hosting;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Gateway.Forwarding;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.Gateway
{
    /// <summary>What the service needs; WCF creates the service per call, so the shared parts live here.</summary>
    public class GatewayServices
    {
        private static readonly Lazy<GatewayServices> Instance = new Lazy<GatewayServices>(Create);

        public static GatewayServices Default
        {
            get { return Instance.Value; }
        }

        public BatchChecker Checker { get; set; }
        public IDuplicateStore Duplicates { get; set; }
        public IBatchSubmitter Submitter { get; set; }
        public TrackingRepository Tracking { get; set; }
        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        private static GatewayServices Create()
        {
            IntegrationDatabase.EnsureFactory();
            var root = HostingEnvironment.IsHosted ? HostingEnvironment.MapPath("~/bin") : AppDomain.CurrentDomain.BaseDirectory;
            var validator = SchemaValidator.FromFolder(Path.Combine(root, "Schemas"));
            var integration = new IntegrationDatabase();
            return new GatewayServices
            {
                Checker = new BatchChecker(validator, new PartnerRepository(integration)),
                Duplicates = new InterchangeRepository(integration),
                Submitter = new BizTalkSubmitter("BizTalkPartnerOrders"),
                Tracking = new TrackingRepository(new IntegrationDatabase("BAMPrimaryImport"))
            };
        }
    }
}
