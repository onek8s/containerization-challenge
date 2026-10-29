using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// Writes a copy of every partner and ERP file to the archive share before (receive) or after (send) the EDI
    /// stages, so operations can find the raw file of any interchange. The relative path goes to VanArsdel.ArchivePath.
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_Any)]
    [Guid("3F8C6A10-91D2-4C7E-A5B4-6E2F1D9C0A44")]
    public class ArchiveComponent : ComponentBase, IComponent
    {
        public ArchiveComponent()
        {
            Enabled = true;
            FileNamePattern = ArchiveNaming.DefaultPattern;
            Direction = "inbound";
        }

        public override string Name
        {
            get { return "VanArsdel Archive"; }
        }

        public override string Description
        {
            get { return "Archives the message body to the archive share."; }
        }

        public bool Enabled { get; set; }

        /// <summary>Overrides the SSO ArchiveRoot (blank = SSO).</summary>
        public string ArchiveRoot { get; set; }

        public string FileNamePattern { get; set; }

        /// <summary>inbound or outbound: first folder level under the root.</summary>
        public string Direction { get; set; }

        public IBaseMessage Execute(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            if (!Enabled)
            {
                return pInMsg;
            }

            var body = MessageBodies.ReadAll(pInMsg);
            var context = new ContextAdapter(pInMsg.Context);
            var root = string.IsNullOrEmpty(ArchiveRoot) ? SsoSettings.Get(SettingNames.ArchiveRoot) : ArchiveRoot;
            var relative = Path.Combine(Direction, ArchiveNaming.Expand(
                FileNamePattern,
                context.ReadVanArsdel(ContextProperties.PartnerId) ?? "unknown",
                context.ReadVanArsdel(ContextProperties.MessageKind) ?? "message",
                pInMsg.MessageID,
                context.ReadString(ContextProperties.ReceivedFileName, ContextProperties.FileNamespace),
                DateTime.UtcNow,
                ArchiveNaming.ExtensionFor(MessageTypeProbe.Probe(body).Format)));

            var full = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, body);
            context.WriteVanArsdel(ContextProperties.ArchivePath, relative);

            MessageBodies.Replace(pContext, pInMsg, body);
            return pInMsg;
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            Enabled = ReadBool(bag, "Enabled", true);
            ArchiveRoot = ReadString(bag, "ArchiveRoot", null);
            FileNamePattern = ReadString(bag, "FileNamePattern", ArchiveNaming.DefaultPattern);
            Direction = ReadString(bag, "Direction", "inbound");
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "Enabled", Enabled);
            WriteProperty(bag, "ArchiveRoot", ArchiveRoot ?? string.Empty);
            WriteProperty(bag, "FileNamePattern", FileNamePattern);
            WriteProperty(bag, "Direction", Direction);
        }
    }
}
