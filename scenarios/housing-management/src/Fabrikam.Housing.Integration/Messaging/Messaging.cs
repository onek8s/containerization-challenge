using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fabrikam.Housing.Integration.Messaging
{
    public interface ISmsSender
    {
        /// <summary>Sends a text and returns the gateway message id.</summary>
        Task<string> SendAsync(string mobileNumber, string text);
    }

    public class EmailAttachment
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
    }

    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody, IEnumerable<EmailAttachment> attachments = null);
    }
}
