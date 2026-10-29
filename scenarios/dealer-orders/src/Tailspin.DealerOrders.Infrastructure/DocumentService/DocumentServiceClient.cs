using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Tailspin.DealerOrders.Infrastructure.DocumentService
{
    /// <summary>
    /// Client for the document service. The endpoint ("DocumentService") is in the caller's system.serviceModel
    /// configuration; every call carries the caller's client key in a SOAP header.
    /// </summary>
    public class DocumentServiceClient
    {
        private readonly string _endpointName;
        private readonly string _clientKey;

        public DocumentServiceClient(string endpointName = "DocumentService")
        {
            _endpointName = endpointName;
            _clientKey = Settings.Required("DocumentService.ClientKey");
        }

        public T Call<T>(Func<IDocumentService, T> call)
        {
            var factory = new ChannelFactory<IDocumentService>(_endpointName);
            var channel = factory.CreateChannel();
            var client = (IClientChannel)channel;
            var ok = false;
            try
            {
                using (new OperationContextScope(client))
                {
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader(DocumentNamespaces.ClientKeyHeader, DocumentNamespaces.Service, _clientKey));
                    var result = call(channel);
                    client.Close();
                    factory.Close();
                    ok = true;
                    return result;
                }
            }
            finally
            {
                if (!ok)
                {
                    client.Abort();
                    factory.Abort();
                }
            }
        }
    }
}
