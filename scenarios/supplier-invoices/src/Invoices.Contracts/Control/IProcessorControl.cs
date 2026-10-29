using System.ServiceModel;

namespace Wingtip.Invoices.Contracts.Control
{
    /// <summary>
    /// Operations of the invoice processor used by the accounts payable web application.
    /// Hosted by the InvoiceProcessor service on net.tcp.
    /// </summary>
    [ServiceContract(Namespace = ControlNamespace.Value)]
    public interface IProcessorControl
    {
        [OperationContract]
        [FaultContract(typeof(ControlFault))]
        QueueStatus[] GetQueueStatus();

        /// <summary>Reads the archived original of an inbound file and queues it again.</summary>
        [OperationContract]
        [FaultContract(typeof(ControlFault))]
        void ResubmitFile(long fileId, string requestedBy);

        /// <summary>Matches an invoice again with current purchase order and receipt data.</summary>
        [OperationContract]
        [FaultContract(typeof(ControlFault))]
        MatchSummary RematchInvoice(long invoiceId, string requestedBy);

        /// <summary>Releases an invoice with exceptions for posting.</summary>
        [OperationContract]
        [FaultContract(typeof(ControlFault))]
        void ReleaseInvoice(long invoiceId, string releasedBy, string comment);
    }

    internal static class ControlNamespace
    {
        public const string Value = "http://schemas.wingtip.fi/invoices/2019/control";
    }
}
