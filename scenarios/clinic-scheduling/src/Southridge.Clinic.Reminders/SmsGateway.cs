using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Reminders
{
    public interface ISmsSender
    {
        /// <summary>Returns the gateway's message id.</summary>
        string Send(string mobilePhone, string text);
    }

    /// <summary>
    /// HTTP SMS gateway (form post, account id + token, reply "OK:&lt;id&gt;" or "ERR:&lt;reason&gt;").
    /// Contracted in 2012; replaces the e-mail-to-SMS addresses used before.
    /// </summary>
    public class HttpSmsGateway : ISmsSender
    {
        private readonly string _url;
        private readonly string _account;
        private readonly string _token;
        private readonly string _senderId;

        public HttpSmsGateway()
            : this(ClinicSettings.Required("Sms:GatewayUrl"), ClinicSettings.Required("Sms:Account"), ClinicSettings.Required("Sms:Token"), ClinicSettings.Get("Sms:SenderId", "SMGCLINIC"))
        {
        }

        public HttpSmsGateway(string url, string account, string token, string senderId)
        {
            _url = url;
            _account = account;
            _token = token;
            _senderId = senderId;
        }

        public string Send(string mobilePhone, string text)
        {
            string to = PhoneNumber.ToE164(mobilePhone);
            if (to == null)
            {
                throw new ClinicRuleException("Not a mobile number: " + mobilePhone);
            }

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "SouthridgeReminders/1.0";
                var form = new NameValueCollection
                {
                    { "account", _account },
                    { "token", _token },
                    { "from", _senderId },
                    { "to", to },
                    { "text", text.Length > 306 ? text.Substring(0, 306) : text }
                };
                string reply = Encoding.UTF8.GetString(client.UploadValues(_url, "POST", form)).Trim();
                if (!reply.StartsWith("OK:", StringComparison.Ordinal))
                {
                    throw new WebException("SMS gateway refused the message: " + reply);
                }

                return reply.Substring(3);
            }
        }
    }
}
