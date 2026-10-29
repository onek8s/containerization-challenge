using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StaffOnboarding.Core.Provisioning;

namespace StaffOnboarding.Workflows.Services
{
    /// <summary>
    /// Calls the directory provisioning service on idm01 with the workflow host's own Kerberos identity.
    /// </summary>
    public class ProvisioningClient : IProvisioningClient, IDisposable
    {
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _http;

        public ProvisioningClient(Uri baseAddress, TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                PreAuthenticate = true,
                AllowAutoRedirect = false
            };
            _http = new HttpClient(handler) { BaseAddress = baseAddress, Timeout = timeout };
            _http.DefaultRequestHeaders.Add("X-Client", "StaffOnboarding.WorkflowHost");
        }

        public Task<ProvisioningResult> CreateAccountAsync(CreateAccountCommand command)
        {
            return PostAsync("api/accounts", command);
        }

        public Task<ProvisioningResult> EnableAccountAsync(EnableAccountCommand command)
        {
            return PostAsync("api/accounts/" + Uri.EscapeDataString(command.SamAccountName) + "/enable", command);
        }

        public Task<ProvisioningResult> UpdateAccessAsync(UpdateAccessCommand command)
        {
            return PostAsync("api/accounts/" + Uri.EscapeDataString(command.SamAccountName) + "/access", command);
        }

        public Task<ProvisioningResult> DisableAccountAsync(DisableAccountCommand command)
        {
            return PostAsync("api/accounts/" + Uri.EscapeDataString(command.SamAccountName) + "/disable", command);
        }

        public Task<ProvisioningResult> ArchiveAccountAsync(ArchiveAccountCommand command)
        {
            return PostAsync("api/accounts/" + Uri.EscapeDataString(command.SamAccountName) + "/archive", command);
        }

        public async Task<AccountSummary> GetAccountAsync(string samAccountName)
        {
            using (var response = await _http.GetAsync("api/accounts/" + Uri.EscapeDataString(samAccountName)).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new ProvisioningUnavailableException("Account lookup for " + samAccountName + " failed: " + (int)response.StatusCode + " " + body);
                return JsonConvert.DeserializeObject<AccountSummary>(body, Json);
            }
        }

        private async Task<ProvisioningResult> PostAsync(string path, ProvisioningCommand command)
        {
            var content = new StringContent(JsonConvert.SerializeObject(command, Json), Encoding.UTF8, "application/json");
            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(path, content).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                return ProvisioningResult.Fail("Provisioning service not reachable: " + ex.GetBaseException().Message, true);
            }
            catch (TaskCanceledException)
            {
                return ProvisioningResult.Fail("Provisioning service did not answer in time.", true);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    return ProvisioningResult.Fail("Provisioning service refused the workflow host account (" + (int)response.StatusCode + ").", false);

                if ((int)response.StatusCode >= 500 && string.IsNullOrEmpty(body))
                    return ProvisioningResult.Fail("Provisioning service error " + (int)response.StatusCode + ".", true);

                var result = JsonConvert.DeserializeObject<ProvisioningResult>(body, Json);
                return result ?? ProvisioningResult.Fail("Empty response from provisioning service.", true);
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }

    [Serializable]
    public class ProvisioningUnavailableException : Exception
    {
        public ProvisioningUnavailableException(string message) : base(message)
        {
        }

        protected ProvisioningUnavailableException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
