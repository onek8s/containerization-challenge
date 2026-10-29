using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Web;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Web.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Contoso.Purchasing.Web.Services
{
    /// <summary>
    /// Calls the purchasing API for the signed-in user.
    /// Delegate: the call runs impersonated so the API sees the end user's Windows identity
    ///           (requires Kerberos constrained delegation from the portal app pool account).
    /// ServiceAccount: the call runs as the app pool identity and names the user in X-Purchasing-User;
    ///           the API must list the app pool account in Purchasing.TrustedCallers.
    /// </summary>
    public class PurchasingApiClient
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Local
        };

        private readonly Uri _baseUri;
        private readonly WindowsIdentity _identity;
        private readonly bool _delegate;

        public PurchasingApiClient(HttpContextBase context)
        {
            _baseUri = new Uri(ConfigurationManager.AppSettings["Purchasing.ApiBaseUrl"]);
            _delegate = !string.Equals(ConfigurationManager.AppSettings["Purchasing.ApiAuthentication"], "ServiceAccount", StringComparison.OrdinalIgnoreCase);
            _identity = context.User.Identity as WindowsIdentity;
            if (_identity == null)
            {
                throw new InvalidOperationException("The portal requires Windows authentication.");
            }
        }

        public UserContext Me()
        {
            return Get<UserContext>("api/me");
        }

        public IList<Supplier> Suppliers() { return Get<List<Supplier>>("api/catalog/suppliers"); }
        public IList<CostCenter> CostCenters() { return Get<List<CostCenter>>("api/catalog/costcenters"); }
        public IList<Category> Categories() { return Get<List<Category>>("api/catalog/categories"); }

        public IList<RequestSummary> Requests(string scope)
        {
            return Get<List<RequestSummary>>("api/requests?scope=" + Uri.EscapeDataString(scope));
        }

        public RequestView Request(int id) { return Get<RequestView>("api/requests/" + id); }
        public RequestView Create(object input) { return Send<RequestView>("POST", "api/requests", input); }
        public RequestView Update(int id, object input) { return Send<RequestView>("PUT", "api/requests/" + id, input); }
        public RequestView Submit(int id) { return Send<RequestView>("POST", "api/requests/" + id + "/submit", null); }
        public RequestView Cancel(int id) { return Send<RequestView>("POST", "api/requests/" + id + "/cancel", null); }

        public RequestView Decide(int id, bool approve, string comment)
        {
            return Send<RequestView>("POST", "api/requests/" + id + "/decision", new { approve, comment });
        }

        public IList<PurchaseOrder> Orders() { return Get<List<PurchaseOrder>>("api/orders"); }
        public PurchaseOrder Order(int id) { return Get<PurchaseOrder>("api/orders/" + id); }

        public PurchaseOrder CreateOrder(int requestId, string deliveryAddress)
        {
            return Send<PurchaseOrder>("POST", "api/orders", new { requestId, deliveryAddress });
        }

        public AttachmentInfo Upload(int requestId, string fileName, string contentType, Stream content)
        {
            var boundary = "----purchasing" + DateTime.Now.Ticks.ToString("x");
            byte[] body;
            using (var buffer = new MemoryStream())
            {
                var header = "--" + boundary + "\r\n" +
                             "Content-Disposition: form-data; name=\"file\"; filename=\"" + fileName.Replace("\"", "") + "\"\r\n" +
                             "Content-Type: " + (contentType ?? "application/octet-stream") + "\r\n\r\n";
                var headerBytes = Encoding.UTF8.GetBytes(header);
                buffer.Write(headerBytes, 0, headerBytes.Length);
                content.CopyTo(buffer);
                var footer = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
                buffer.Write(footer, 0, footer.Length);
                body = buffer.ToArray();
            }

            return Execute(client =>
            {
                client.Headers[HttpRequestHeader.ContentType] = "multipart/form-data; boundary=" + boundary;
                var response = client.UploadData(new Uri(_baseUri, "api/requests/" + requestId + "/attachments"), "POST", body);
                return JsonConvert.DeserializeObject<AttachmentInfo>(Encoding.UTF8.GetString(response), JsonSettings);
            });
        }

        public byte[] Download(int attachmentId, out string contentType, out string fileName)
        {
            string type = null, name = null;
            var data = Execute(client =>
            {
                var bytes = client.DownloadData(new Uri(_baseUri, "api/attachments/" + attachmentId));
                type = client.ResponseHeaders[HttpResponseHeader.ContentType];
                var disposition = client.ResponseHeaders["Content-Disposition"];
                if (disposition != null)
                {
                    var index = disposition.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
                    if (index >= 0) name = disposition.Substring(index + 9).Trim('"', ' ');
                }
                return bytes;
            });
            contentType = type ?? "application/octet-stream";
            fileName = name ?? ("attachment-" + attachmentId);
            return data;
        }

        private T Get<T>(string path)
        {
            return Execute(client => JsonConvert.DeserializeObject<T>(client.DownloadString(new Uri(_baseUri, path)), JsonSettings));
        }

        private T Send<T>(string method, string path, object body)
        {
            return Execute(client =>
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                var json = body == null ? "" : JsonConvert.SerializeObject(body, JsonSettings);
                var response = client.UploadString(new Uri(_baseUri, path), method, json);
                return JsonConvert.DeserializeObject<T>(response, JsonSettings);
            });
        }

        private T Execute<T>(Func<WebClient, T> call)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                Func<T> invoke = () =>
                {
                    using (var client = new WebClient { UseDefaultCredentials = true, Encoding = Encoding.UTF8 })
                    {
                        client.Headers[HttpRequestHeader.Accept] = "application/json";
                        if (!_delegate) client.Headers["X-Purchasing-User"] = _identity.Name;
                        return call(client);
                    }
                };

                return _delegate ? WindowsIdentity.RunImpersonated(_identity.AccessToken, invoke) : invoke();
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null)
                {
                    Trace.TraceError("Purchasing API not reachable at {0}: {1}", _baseUri, ex.Message);
                    throw new ApiException(HttpStatusCode.ServiceUnavailable, "The purchasing service is not available. Please try again later.");
                }

                string message;
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    message = ReadMessage(reader.ReadToEnd());
                }
                throw new ApiException(response.StatusCode, message ?? response.StatusDescription);
            }
            finally
            {
                Trace.WriteLine(string.Format("API call as {0} took {1} ms", _identity.Name, watch.ElapsedMilliseconds), "Purchasing.Web");
            }
        }

        private static string ReadMessage(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var error = JsonConvert.DeserializeAnonymousType(body, new { Message = "" });
                return error.Message;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
