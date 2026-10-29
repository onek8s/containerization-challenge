using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Coho.Billing.Core.Runs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Coho.Billing.Monitor
{
    public class EngineStatusView
    {
        public string Version { get; set; }
        public string Machine { get; set; }
        public DateTime StartedAt { get; set; }
        public bool Paused { get; set; }
        public string PausedBy { get; set; }
        public bool Busy { get; set; }
        public string CurrentWork { get; set; }
        public DateTime? LastCycleAt { get; set; }
        public DateTime? LastImportAt { get; set; }
        public string LastError { get; set; }
    }

    /// <summary>Client for the engine control endpoint, authenticated as the signed-in operator.</summary>
    public sealed class EngineClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerSettings _json = new JsonSerializerSettings { Converters = { new StringEnumConverter() } };

        public EngineClient(string baseUrl)
        {
            _http = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true, PreAuthenticate = true })
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public Task<EngineStatusView> GetStatusAsync()
        {
            return SendAsync<EngineStatusView>(HttpMethod.Get, "api/status", null);
        }

        public Task<EngineStatusView> PauseAsync()
        {
            return SendAsync<EngineStatusView>(HttpMethod.Post, "api/engine/pause", null);
        }

        public Task<EngineStatusView> ContinueAsync()
        {
            return SendAsync<EngineStatusView>(HttpMethod.Post, "api/engine/continue", null);
        }

        public Task<EngineStatusView> ScanAsync()
        {
            return SendAsync<EngineStatusView>(HttpMethod.Post, "api/import/scan", null);
        }

        public Task RunCommandAsync(int runId, string action)
        {
            return SendAsync<object>(HttpMethod.Post, "api/runs/" + runId + "/" + action, null);
        }

        public Task ResolveAsync(int exceptionId, ExceptionResolution resolution, string note)
        {
            return SendAsync<object>(HttpMethod.Post, "api/exceptions/" + exceptionId + "/resolve", new { Resolution = resolution, Note = note });
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body)
        {
            using (var request = new HttpRequestMessage(method, path))
            {
                if (body != null)
                {
                    request.Content = new StringContent(JsonConvert.SerializeObject(body, _json), Encoding.UTF8, "application/json");
                }
                else if (method == HttpMethod.Post)
                {
                    request.Content = new StringContent(string.Empty);
                }

                using (var response = await _http.SendAsync(request).ConfigureAwait(false))
                {
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new EngineCallException(response.StatusCode, ErrorMessage(text) ?? response.ReasonPhrase);
                    }

                    return JsonConvert.DeserializeObject<T>(text, _json);
                }
            }
        }

        private static string ErrorMessage(string text)
        {
            try
            {
                var error = JsonConvert.DeserializeAnonymousType(text, new { Message = string.Empty });
                return error == null ? null : error.Message;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }

    public class EngineCallException : Exception
    {
        public EngineCallException(HttpStatusCode status, string message)
            : base(message)
        {
            Status = status;
        }

        public HttpStatusCode Status { get; private set; }
    }
}
