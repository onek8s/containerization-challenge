using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using log4net;
using VanArsdel.Integration.Contracts.Gateway;

namespace VanArsdel.Integration.Gateway.Forwarding
{
    public interface IBatchSubmitter
    {
        void Submit(OrderBatch batch, string receiptId);
    }

    /// <summary>Untyped one-way contract of the BizTalk-published receive location.</summary>
    [ServiceContract(Namespace = "http://schemas.vanarsdel.com/biztalk/receive/2016", Name = "PartnerOrders")]
    public interface IBizTalkOneWay
    {
        [OperationContract(Action = "SubmitOrderBatch", IsOneWay = true)]
        void Submit(Message message);
    }

    /// <summary>
    /// Sends an accepted batch to rp_Gateway_Orders (WCF-BasicHttp receive location in the BizTalk isolated host,
    /// same IIS site, Windows transport credentials of the app pool account). The receipt id travels in a SOAP
    /// header; the receive location promotes it to the GatewayReceiptId context property.
    /// </summary>
    public class BizTalkSubmitter : IBatchSubmitter, IDisposable
    {
        public const string ReceiptHeader = "GatewayReceiptId";
        public const string HeaderNamespace = "http://schemas.vanarsdel.com/biztalk/receive/2016";
        private static readonly ILog Log = LogManager.GetLogger(typeof(BizTalkSubmitter));
        private readonly ChannelFactory<IBizTalkOneWay> _factory;

        public BizTalkSubmitter(string endpointConfigurationName)
        {
            _factory = new ChannelFactory<IBizTalkOneWay>(endpointConfigurationName);
        }

        public BizTalkSubmitter(Binding binding, EndpointAddress address)
        {
            _factory = new ChannelFactory<IBizTalkOneWay>(binding, address);
        }

        public void Submit(OrderBatch batch, string receiptId)
        {
            var body = BatchChecker.Serialize(batch);
            var channel = _factory.CreateChannel();
            var client = (IClientChannel)channel;
            try
            {
                using (var reader = XmlReader.Create(new MemoryStream(body)))
                {
                    var message = Message.CreateMessage(_factory.Endpoint.Binding.MessageVersion, "SubmitOrderBatch", reader);
                    message.Headers.Add(MessageHeader.CreateHeader(ReceiptHeader, HeaderNamespace, receiptId));
                    channel.Submit(message);
                }

                client.Close();
            }
            catch (CommunicationException ex)
            {
                client.Abort();
                Log.Error("BizTalk did not accept batch " + batch.BatchId, ex);
                throw new FaultException<GatewayFault>(new GatewayFault { Code = "Unavailable", Message = "Orders cannot be received right now; try again in a few minutes." },
                    new FaultReason("Service unavailable"), new FaultCode("Unavailable"));
            }
            catch (TimeoutException ex)
            {
                client.Abort();
                Log.Error("BizTalk timed out on batch " + batch.BatchId, ex);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _factory.Close();
            }
            catch (CommunicationException)
            {
                _factory.Abort();
            }
        }
    }
}
