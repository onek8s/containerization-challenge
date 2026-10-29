using System.IO;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using Microsoft.BizTalk.Streaming;

namespace VanArsdel.Integration.PipelineComponents.Common
{
    /// <summary>Body stream helpers. Partner documents are small (a few MB at most), so components work on byte arrays.</summary>
    public static class MessageBodies
    {
        public static byte[] ReadAll(IBaseMessage message)
        {
            var stream = message.BodyPart.GetOriginalDataStream();
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>Replaces the body with the given bytes; the stream is tracked so BizTalk disposes it.</summary>
        public static void Replace(IPipelineContext context, IBaseMessage message, byte[] body)
        {
            var stream = new VirtualStream(VirtualStream.MemoryFlag.AutoOverFlowToDisk);
            stream.Write(body, 0, body.Length);
            stream.Position = 0;
            context.ResourceTracker.AddResource(stream);
            message.BodyPart.Data = stream;
        }

        /// <summary>Puts a read-only seekable wrapper around the body so later components can read it again.</summary>
        public static Stream MakeSeekable(IPipelineContext context, IBaseMessage message)
        {
            var original = message.BodyPart.GetOriginalDataStream();
            if (original.CanSeek)
            {
                return original;
            }

            var seekable = new ReadOnlySeekableStream(original);
            context.ResourceTracker.AddResource(seekable);
            message.BodyPart.Data = seekable;
            return seekable;
        }

        public static IBaseMessage CreateMessage(IPipelineContext context, IBaseMessage template, byte[] body)
        {
            var factory = context.GetMessageFactory();
            var message = factory.CreateMessage();
            message.Context = PipelineUtil.CloneMessageContext(template.Context);
            var part = factory.CreateMessagePart();
            part.Charset = "utf-8";
            part.ContentType = "text/xml";
            var stream = new MemoryStream(body);
            context.ResourceTracker.AddResource(stream);
            part.Data = stream;
            message.AddPart("body", part, true);
            return message;
        }
    }
}
