using System;
using System.IO;
using System.Text;

namespace VanArsdel.Integration.Messaging
{
    /// <summary>A body plus context, the unit the stream components in this library work on.</summary>
    public class PipelineMessage
    {
        public PipelineMessage(byte[] body, IMessageContext context)
        {
            Body = body ?? new byte[0];
            Context = context ?? new MessageContext();
            MessageId = Guid.NewGuid();
        }

        public Guid MessageId { get; set; }
        public byte[] Body { get; set; }
        public IMessageContext Context { get; private set; }
        public string Charset { get; set; }

        public Stream OpenBody()
        {
            return new MemoryStream(Body, false);
        }

        public string BodyText
        {
            get { return Encoding.UTF8.GetString(Body); }
        }

        public static PipelineMessage FromText(string text, IMessageContext context = null)
        {
            return new PipelineMessage(new UTF8Encoding(false).GetBytes(text ?? string.Empty), context) { Charset = "utf-8" };
        }
    }
}
