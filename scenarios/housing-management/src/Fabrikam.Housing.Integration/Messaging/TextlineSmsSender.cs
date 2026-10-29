using System;
using System.Threading.Tasks;
using Fabrikam.Housing.Communications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Fabrikam.Housing.Integration.Messaging
{
    public class TextlineSmsSender : ISmsSender
    {
        private readonly IRestClient _client;
        private readonly SmsOptions _options;
        private readonly ILogger<TextlineSmsSender> _logger;

        public TextlineSmsSender(RestClientFactory factory, IOptions<IntegrationOptions> options, ILogger<TextlineSmsSender> logger)
        {
            _options = options.Value.Sms;
            _logger = logger;
            _client = factory.Create(_options.BaseUrl);
        }

        public async Task<string> SendAsync(string mobileNumber, string text)
        {
            var request = new RestRequest("v1/messages", Method.POST);
            request.AddHeader("X-Api-Key", _options.ApiKey);
            request.AddJsonBody(new
            {
                from = _options.SenderId,
                to = ToInternational(mobileNumber),
                body = SmsTemplates.Truncate(text)
            });
            var response = await RestClientFactory.RetryPolicy(_logger, "Textline").ExecuteAsync(() => _client.ExecuteAsync(request));
            RestClientFactory.EnsureSuccess(response, "Textline");
            var id = (string)JObject.Parse(response.Content)["message_id"];
            _logger.LogInformation("SMS {MessageId} sent to {Number}", id, Mask(mobileNumber));
            return id;
        }

        /// <summary>07700 900123 -> +447700900123</summary>
        public static string ToInternational(string mobile)
        {
            var digits = (mobile ?? "").Replace(" ", "").Replace("-", "");
            if (digits.StartsWith("+")) return digits;
            if (digits.StartsWith("00")) return "+" + digits.Substring(2);
            if (digits.StartsWith("0")) return "+44" + digits.Substring(1);
            throw new ArgumentException("Not a UK mobile number: " + mobile);
        }

        public static string Mask(string mobile) => string.IsNullOrEmpty(mobile) || mobile.Length < 4 ? mobile : "*******" + mobile.Substring(mobile.Length - 3);
    }
}
