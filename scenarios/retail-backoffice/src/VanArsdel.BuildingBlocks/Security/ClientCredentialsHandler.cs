using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VanArsdel.BuildingBlocks.Security
{
    public class ServiceIdentityOptions
    {
        public string TokenEndpoint { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        /// <summary>Entra ID uses "api://vanarsdel-retail/.default"; ADFS uses the resource identifier.</summary>
        public string Scope { get; set; }

        /// <summary>ADFS 2016 and later still accept a "resource" parameter.</summary>
        public string Resource { get; set; }
    }

    /// <summary>Caches one client credentials token per process.</summary>
    public class ServiceTokenProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<ServiceIdentityOptions> _options;
        private readonly ILogger<ServiceTokenProvider> _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private string _token;
        private DateTime _expiresUtc;

        public ServiceTokenProvider(IHttpClientFactory httpClientFactory, IOptions<ServiceIdentityOptions> options, ILogger<ServiceTokenProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            if (_token != null && DateTime.UtcNow < _expiresUtc) return _token;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_token != null && DateTime.UtcNow < _expiresUtc) return _token;

                var o = _options.Value;
                var request = new ClientCredentialsTokenRequest
                {
                    Address = o.TokenEndpoint,
                    ClientId = o.ClientId,
                    ClientSecret = o.ClientSecret,
                    Scope = o.Scope,
                };
                if (!string.IsNullOrEmpty(o.Resource)) request.Resource.Add(o.Resource);

                var client = _httpClientFactory.CreateClient("token");
                var response = await client.RequestClientCredentialsTokenAsync(request, cancellationToken);
                if (response.IsError)
                {
                    _logger.LogError("Token request to {TokenEndpoint} failed: {Error} {Description}", o.TokenEndpoint, response.Error, response.ErrorDescription);
                    throw new InvalidOperationException("Could not get a service token: " + response.Error);
                }

                _token = response.AccessToken;
                _expiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, response.ExpiresIn - 120));
                return _token;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public class ClientCredentialsHandler : DelegatingHandler
    {
        private readonly ServiceTokenProvider _tokens;

        public ClientCredentialsHandler(ServiceTokenProvider tokens)
        {
            _tokens = tokens;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _tokens.GetTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
