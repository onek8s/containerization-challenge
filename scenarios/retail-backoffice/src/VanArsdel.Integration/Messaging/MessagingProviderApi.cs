using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Refit;

namespace VanArsdel.Integration.Messaging
{
    /// <summary>Postiq transactional e-mail API.</summary>
    public interface IEmailProviderApi
    {
        [Post("/v3/mail/send")]
        Task<EmailSendResponse> SendAsync([Body] EmailSendRequest request, [Header("Authorization")] string authorization);
    }

    /// <summary>Postiq SMS API.</summary>
    public interface ISmsProviderApi
    {
        [Post("/v1/sms")]
        Task<SmsSendResponse> SendAsync([Body] SmsSendRequest request, [Header("Authorization")] string authorization);
    }

    public class EmailSendRequest
    {
        [JsonProperty("from")] public EmailAddress From { get; set; }
        [JsonProperty("to")] public List<EmailAddress> To { get; set; } = new List<EmailAddress>();
        [JsonProperty("subject")] public string Subject { get; set; }
        [JsonProperty("html")] public string Html { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("tags")] public List<string> Tags { get; set; } = new List<string>();
        [JsonProperty("attachments")] public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }

    public class EmailAddress
    {
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }

    public class EmailAttachment
    {
        [JsonProperty("filename")] public string FileName { get; set; }
        [JsonProperty("contentType")] public string ContentType { get; set; }
        [JsonProperty("content")] public string ContentBase64 { get; set; }
    }

    public class EmailSendResponse
    {
        [JsonProperty("messageId")] public string MessageId { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
    }

    public class SmsSendRequest
    {
        [JsonProperty("from")] public string From { get; set; }
        [JsonProperty("to")] public string To { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
    }

    public class SmsSendResponse
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("parts")] public int Parts { get; set; }
    }
}
