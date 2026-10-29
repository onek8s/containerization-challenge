using Coho.Billing.Core.Runs;

namespace Coho.Billing.Engine.Control
{
    public class ResolveRequest
    {
        public ExceptionResolution Resolution { get; set; }
        public string Note { get; set; }
    }

    public class ReprintRequestModel
    {
        public string BillNumber { get; set; }
        public string RequestedBy { get; set; }
        public string Reason { get; set; }
    }

    public class RunAction
    {
        public int RunId { get; set; }
        public string Action { get; set; }
        public string Stage { get; set; }
    }
}
