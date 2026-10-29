using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Fabrikam.Housing.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;

namespace Fabrikam.Housing.Integration.DocumentStore
{
    public class DocumentServiceClient : IDocumentServiceClient
    {
        public const string ApiKeyHeader = "X-Api-Key";
        private readonly IRestClient _client;
        private readonly DocumentServiceOptions _options;
        private readonly ILogger<DocumentServiceClient> _logger;

        public DocumentServiceClient(RestClientFactory factory, IOptions<IntegrationOptions> options, ILogger<DocumentServiceClient> logger)
        {
            _options = options.Value.DocumentService;
            _logger = logger;
            _client = factory.Create(_options.BaseUrl, internalHost: true);
            _client.Timeout = _options.TimeoutSeconds * 1000;
            _client.AddDefaultHeader(ApiKeyHeader, _options.ApiKey ?? "");
        }

        public async Task<DocumentRecord> UploadAsync(DocumentUpload upload, Stream content)
        {
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await content.CopyToAsync(ms);
                bytes = ms.ToArray();
            }
            var request = new RestRequest("api/documents", Method.POST) { AlwaysMultipartFormData = true };
            request.AddParameter("category", upload.Category.ToString());
            request.AddParameter("title", upload.Title ?? upload.FileName);
            Optional(request, "propertyId", upload.PropertyId);
            Optional(request, "tenancyId", upload.TenancyId);
            Optional(request, "workOrderId", upload.WorkOrderId);
            Optional(request, "contractorId", upload.ContractorId);
            request.AddParameter("visibleToTenant", upload.VisibleToTenant ? "true" : "false");
            request.AddParameter("uploadedBy", upload.UploadedBy ?? "system");
            request.AddFile("file", bytes, upload.FileName, upload.ContentType ?? "application/octet-stream");

            var response = await _client.ExecuteAsync(request);
            RestClientFactory.EnsureSuccess(response, "DocumentService");
            return JsonConvert.DeserializeObject<DocumentRecord>(response.Content);
        }

        public Task<DocumentContent> DownloadAsync(Guid documentKey) => GetFileAsync($"api/documents/{documentKey:D}/content");

        public Task<DocumentContent> ThumbnailAsync(Guid documentKey) => GetFileAsync($"api/documents/{documentKey:D}/thumbnail");

        public async Task<IList<SearchHit>> SearchAsync(string query, int? tenancyId = null, int? propertyId = null, int max = 50)
        {
            var request = new RestRequest("api/documents/search", Method.GET);
            request.AddQueryParameter("q", query ?? "");
            if (tenancyId.HasValue) request.AddQueryParameter("tenancyId", tenancyId.Value.ToString());
            if (propertyId.HasValue) request.AddQueryParameter("propertyId", propertyId.Value.ToString());
            request.AddQueryParameter("max", max.ToString());
            var response = await RestClientFactory.RetryPolicy(_logger, "DocumentService").ExecuteAsync(() => _client.ExecuteAsync(request));
            RestClientFactory.EnsureSuccess(response, "DocumentService");
            return JsonConvert.DeserializeObject<List<SearchHit>>(response.Content);
        }

        public async Task DeleteAsync(Guid documentKey, string reason)
        {
            var request = new RestRequest($"api/documents/{documentKey:D}", Method.DELETE);
            request.AddQueryParameter("reason", reason ?? "");
            var response = await _client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound) return;
            RestClientFactory.EnsureSuccess(response, "DocumentService");
        }

        private async Task<DocumentContent> GetFileAsync(string resource)
        {
            var request = new RestRequest(resource, Method.GET);
            var response = await RestClientFactory.RetryPolicy(_logger, "DocumentService").ExecuteAsync(() => _client.ExecuteAsync(request));
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            RestClientFactory.EnsureSuccess(response, "DocumentService");
            return new DocumentContent
            {
                Content = response.RawBytes,
                ContentType = response.ContentType,
                FileName = FileNameFrom(response) ?? "document"
            };
        }

        private static string FileNameFrom(IRestResponse response)
        {
            foreach (var h in response.Headers)
            {
                if (!string.Equals(h.Name, "Content-Disposition", StringComparison.OrdinalIgnoreCase)) continue;
                var value = h.Value?.ToString() ?? "";
                var i = value.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
                if (i >= 0) return value.Substring(i + 9).Split(';')[0].Trim('"', ' ');
            }
            return null;
        }

        private static void Optional(IRestRequest request, string name, int? value)
        {
            if (value.HasValue) request.AddParameter(name, value.Value.ToString());
        }
    }
}
