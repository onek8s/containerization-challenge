using System;
using System.Collections.Generic;

namespace Wingtip.Invoices.Gateway.Channels
{
    public class InboundItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime? Modified { get; set; }
    }

    /// <summary>A supplier delivery location: an FTP folder or a folder on the drop share.</summary>
    public interface IInboundChannel
    {
        string Description { get; }

        IList<InboundItem> List();

        byte[] Read(InboundItem item);

        /// <summary>Moves the file out of the delivery folder after it was queued or rejected.</summary>
        void Complete(InboundItem item, bool accepted);
    }
}
