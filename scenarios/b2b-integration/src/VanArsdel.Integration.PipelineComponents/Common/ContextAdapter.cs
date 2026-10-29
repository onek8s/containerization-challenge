using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.PipelineComponents.Common
{
    /// <summary>The BizTalk message context behind the IMessageContext the shared stream logic works with.</summary>
    public class ContextAdapter : IMessageContext
    {
        private readonly IBaseMessageContext _context;

        public ContextAdapter(IBaseMessageContext context)
        {
            _context = context;
        }

        public object Read(string name, string ns)
        {
            return _context.Read(name, ns);
        }

        public void Write(string name, string ns, object value)
        {
            _context.Write(name, ns, value);
        }

        public void Promote(string name, string ns, object value)
        {
            _context.Promote(name, ns, value);
        }

        public bool IsPromoted(string name, string ns)
        {
            return _context.IsPromoted(name, ns);
        }
    }
}
