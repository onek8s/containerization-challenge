using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Activation;
using log4net;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Documents.Excel;
using Tailspin.DealerOrders.Documents.Word;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Infrastructure.DocumentService;
using Tailspin.DealerOrders.Processing.Ports;
using Unity;

namespace Tailspin.DealerOrders.DocumentService
{
    [ServiceBehavior(Namespace = DocumentNamespaces.Service, InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class DocumentService : IDocumentService, IDisposable
    {
        private const string WordType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentService));

        private readonly IUnityContainer _scope = ServiceRuntime.Container.CreateChildContainer();

        public DocumentInfo GenerateQuote(int quoteId, string requestedBy) => Run(nameof(GenerateQuote), () =>
        {
            var quote = _scope.Resolve<IQuoteStore>().Get(quoteId) ?? throw Fault("NotFound", $"Quote {quoteId} does not exist.");
            var dealer = Dealer(quote.DealerId);
            var content = _scope.Resolve<SalesDocumentBuilder>().Quote(_scope.Resolve<DocumentModels>().ForQuote(quote, dealer));
            return Store(DocumentKind.Quote, dealer.Id, quoteId, null, quote.QuoteNumber + ".docx", WordType, content, requestedBy);
        });

        public DocumentInfo GenerateOrderConfirmation(int orderId, string requestedBy) => Run(nameof(GenerateOrderConfirmation), () =>
        {
            var order = _scope.Resolve<IOrderStore>().Get(orderId) ?? throw Fault("NotFound", $"Order {orderId} does not exist.");
            if (order.Status < OrderStatus.Confirmed || order.Status >= OrderStatus.Cancelled)
                throw Fault("NotConfirmed", $"Order {order.OrderNumber} is not confirmed ({order.Status}).");
            var dealer = Dealer(order.DealerId);
            var content = _scope.Resolve<SalesDocumentBuilder>().OrderConfirmation(_scope.Resolve<DocumentModels>().ForOrder(order, dealer));
            return Store(DocumentKind.OrderConfirmation, dealer.Id, null, orderId, order.OrderNumber + ".docx", WordType, content, requestedBy);
        });

        public DocumentInfo GenerateDealerContract(int dealerId, int year, string requestedBy) => Run(nameof(GenerateDealerContract), () =>
        {
            if (year < 2014 || year > DateTime.Today.Year + 1) throw Fault("InvalidYear", $"No agreement can be made for {year}.");
            var dealer = Dealer(dealerId);
            var content = _scope.Resolve<SalesDocumentBuilder>().DealerContract(_scope.Resolve<DocumentModels>().ForContract(dealer, year));
            return Store(DocumentKind.DealerContract, dealer.Id, null, null, $"Dealer-agreement-{dealer.AccountNumber}-{year}.docx", WordType, content, requestedBy);
        });

        public DocumentInfo GeneratePriceList(int dealerId, string requestedBy) => Run(nameof(GeneratePriceList), () =>
        {
            var dealer = Dealer(dealerId);
            var today = _scope.Resolve<IClock>().Today;
            var models = _scope.Resolve<ICatalog>().All().Where(m => m.IsValidOn(today));
            var content = _scope.Resolve<PriceListWorkbook>().Build(dealer, models, today);
            return Store(DocumentKind.PriceList, dealer.Id, null, null, $"Pricelist-{dealer.AccountNumber}-{today:yyyyMMdd}.xlsx", ExcelType, content, requestedBy);
        });

        public DocumentContent GetDocument(int documentId) => Run(nameof(GetDocument), () =>
        {
            var doc = _scope.Resolve<SqlDocumentStore>().Get(documentId) ?? throw Fault("NotFound", $"Document {documentId} does not exist.");
            return new DocumentContent { Id = doc.Id, DealerId = doc.DealerId, FileName = doc.FileName, ContentType = doc.ContentType, Content = doc.Content };
        });

        public string Ping() => $"Tailspin document service {typeof(DocumentService).Assembly.GetName().Version} on {Environment.MachineName}";

        public void Dispose() => _scope.Dispose();

        private Dealer Dealer(int id) => _scope.Resolve<IDealerStore>().Get(id) ?? throw Fault("NotFound", $"Dealer {id} does not exist.");

        private DocumentInfo Store(DocumentKind kind, int dealerId, int? quoteId, int? orderId, string fileName, string contentType, byte[] content, string requestedBy)
        {
            var doc = _scope.Resolve<SqlDocumentStore>().Add(new StoredDocument
            {
                Kind = kind,
                DealerId = dealerId,
                QuoteId = quoteId,
                OrderId = orderId,
                FileName = fileName,
                ContentType = contentType,
                Content = content,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = string.IsNullOrEmpty(requestedBy) ? ClientName : requestedBy
            });
            Log.InfoFormat("{0} {1} ({2:N0} bytes) for {3} via {4}", kind, fileName, content.Length, doc.CreatedBy, ClientName);
            return new DocumentInfo { Id = doc.Id, FileName = doc.FileName, ContentType = doc.ContentType, Size = content.Length, Sha256 = doc.Sha256, CreatedUtc = doc.CreatedUtc };
        }

        private string ClientName { get; set; }

        private T Run<T>(string operation, Func<T> work)
        {
            ClientName = Authorize();
            try
            {
                return work();
            }
            catch (FaultException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"{operation} failed", ex);
                throw Fault("Failed", "The document could not be created. The error is in the document service log.");
            }
        }

        /// <summary>Every caller sends its client key in the ClientKey SOAP header (see DocumentServiceClient).</summary>
        private static string Authorize()
        {
            var headers = OperationContext.Current.IncomingMessageHeaders;
            var index = headers.FindHeader(DocumentNamespaces.ClientKeyHeader, DocumentNamespaces.Service);
            var key = index < 0 ? null : headers.GetHeader<string>(index);
            if (key == null || !ServiceRuntime.Clients.TryGetValue(key, out var client))
            {
                Log.WarnFormat("Call without a valid client key refused");
                throw Fault("Unauthorized", "Missing or unknown client key.");
            }
            return client;
        }

        private static FaultException<DocumentFault> Fault(string code, string message) =>
            new FaultException<DocumentFault>(new DocumentFault { Code = code, Message = message }, new FaultReason(message));
    }
}
