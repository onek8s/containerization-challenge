using System;
using System.Net;
using Lucerne.ProjectSites.Contracts.DocumentControl;
using Newtonsoft.Json;
using RestSharp;

namespace Lucerne.ProjectSites.Integration.DocumentControl
{
    public interface IDocumentControlGateway
    {
        SubmissionReceipt Submit(DocumentSubmission submission, byte[] content);
        SubmissionStatus GetStatus(string submissionId);
    }

    /// <summary>
    /// Client of the document-control system API. OAuth2 client credentials; the token is cached until it
    /// expires. Submissions are multipart: metadata JSON + the file.
    /// </summary>
    public class DocumentControlClient : IDocumentControlGateway
    {
        private readonly DocumentControlSettings _settings;
        private readonly IRestClient _client;
        private readonly Action<string> _trace;
        private string _token;
        private DateTime _tokenExpires;

        public DocumentControlClient(DocumentControlSettings settings, Action<string> trace = null)
            : this(settings, new RestClient(settings.BaseUrl), trace)
        {
        }

        public DocumentControlClient(DocumentControlSettings settings, IRestClient client, Action<string> trace)
        {
            _settings = settings;
            _client = client;
            _trace = trace ?? (_ => { });
        }

        public SubmissionReceipt Submit(DocumentSubmission submission, byte[] content)
        {
            var request = new RestRequest("api/v1/submissions", Method.POST) { AlwaysMultipartFormData = true };
            request.AddHeader("Authorization", "Bearer " + GetToken());
            request.AddParameter("metadata", JsonConvert.SerializeObject(submission), ParameterType.GetOrPost);
            request.AddFile("file", content, submission.FileName, "application/octet-stream");

            var response = Execute(request, "submit " + submission.DocumentNumber);
            if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.OK)
            {
                throw new IntegrationException("Document control rejected " + submission.DocumentNumber + ": " + (int)response.StatusCode + " " + response.Content);
            }

            return JsonConvert.DeserializeObject<SubmissionReceipt>(response.Content);
        }

        public SubmissionStatus GetStatus(string submissionId)
        {
            var request = new RestRequest("api/v1/submissions/{id}", Method.GET);
            request.AddUrlSegment("id", submissionId);
            request.AddHeader("Authorization", "Bearer " + GetToken());
            var response = Execute(request, "status " + submissionId);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new IntegrationException("Document control status " + submissionId + ": " + (int)response.StatusCode);
            }

            return JsonConvert.DeserializeObject<SubmissionStatus>(response.Content);
        }

        private string GetToken()
        {
            if (_token != null && DateTime.UtcNow < _tokenExpires)
            {
                return _token;
            }

            var request = new RestRequest("oauth/token", Method.POST);
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", _settings.ClientId);
            request.AddParameter("client_secret", _settings.ClientSecret);
            request.AddParameter("scope", "submissions");
            var response = Execute(request, "token");
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new IntegrationException("Document control token request failed: " + (int)response.StatusCode);
            }

            var token = JsonConvert.DeserializeAnonymousType(response.Content, new { access_token = string.Empty, expires_in = 0 });
            _token = token.access_token;
            _tokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(60, token.expires_in - 60));
            return _token;
        }

        private IRestResponse Execute(IRestRequest request, string what)
        {
            return RetryPolicies.Rest((reason, delay) => _trace("Document control " + what + " failed (" + reason + "), retrying in " + delay))
                .Execute(() => _client.Execute(request));
        }
    }
}
