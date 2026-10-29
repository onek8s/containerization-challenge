using System;
using System.Collections.Generic;
using System.Net;
using Lucerne.ProjectSites.Contracts.Hr;
using Newtonsoft.Json;
using RestSharp;

namespace Lucerne.ProjectSites.Integration.Hr
{
    public interface IHrDirectory
    {
        IEnumerable<HrEmployee> GetEmployeesChangedSince(DateTime? sinceUtc);
    }

    /// <summary>HR system REST API v2. API key in the X-Api-Key header, paging by page number.</summary>
    public class HrDirectoryClient : IHrDirectory
    {
        private readonly HrSettings _settings;
        private readonly IRestClient _client;
        private readonly Action<string> _trace;

        public HrDirectoryClient(HrSettings settings, Action<string> trace = null)
            : this(settings, new RestClient(settings.BaseUrl), trace)
        {
        }

        public HrDirectoryClient(HrSettings settings, IRestClient client, Action<string> trace)
        {
            _settings = settings;
            _client = client;
            _trace = trace ?? (_ => { });
            _client.Timeout = 60000;
        }

        public IEnumerable<HrEmployee> GetEmployeesChangedSince(DateTime? sinceUtc)
        {
            var page = 1;
            while (true)
            {
                var request = new RestRequest("api/v2/employees", Method.GET);
                request.AddHeader("X-Api-Key", _settings.ApiKey ?? string.Empty);
                request.AddQueryParameter("page", page.ToString());
                request.AddQueryParameter("pageSize", _settings.PageSize.ToString());
                if (sinceUtc.HasValue)
                {
                    request.AddQueryParameter("modifiedSince", sinceUtc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                }

                var current = page;
                var response = RetryPolicies.Rest((what, delay) => _trace("HR page " + current + " failed (" + what + "), retrying in " + delay))
                    .Execute(() => _client.Execute(request));

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new IntegrationException("HR API returned " + (int)response.StatusCode + " " + response.StatusDescription + ": " + response.ErrorMessage);
                }

                var result = JsonConvert.DeserializeObject<HrEmployeePage>(response.Content);
                _trace("HR employees page " + page + ": " + result.Items.Count + " of " + result.Total);
                foreach (var e in result.Items)
                {
                    yield return e;
                }

                if (page * result.PageSize >= result.Total || result.Items.Count == 0)
                {
                    yield break;
                }

                page++;
            }
        }
    }
}
