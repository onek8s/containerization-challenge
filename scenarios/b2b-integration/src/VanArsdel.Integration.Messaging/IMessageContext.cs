using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Messaging
{
    /// <summary>
    /// The part of the BizTalk message context the stream components use. In BizTalk it wraps IBaseMessageContext
    /// (PipelineComponents.ContextAdapter); the pipeline runner and the tests use MessageContext.
    /// </summary>
    public interface IMessageContext
    {
        object Read(string name, string ns);
        void Write(string name, string ns, object value);
        void Promote(string name, string ns, object value);
        bool IsPromoted(string name, string ns);
    }

    public class MessageContext : IMessageContext
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly HashSet<string> _promoted = new HashSet<string>(StringComparer.Ordinal);

        public object Read(string name, string ns)
        {
            object value;
            return _values.TryGetValue(Key(name, ns), out value) ? value : null;
        }

        public void Write(string name, string ns, object value)
        {
            var key = Key(name, ns);
            if (value == null)
            {
                _values.Remove(key);
                _promoted.Remove(key);
                return;
            }

            _values[key] = value;
        }

        public void Promote(string name, string ns, object value)
        {
            Write(name, ns, value);
            if (value != null)
            {
                _promoted.Add(Key(name, ns));
            }
        }

        public bool IsPromoted(string name, string ns)
        {
            return _promoted.Contains(Key(name, ns));
        }

        public IEnumerable<KeyValuePair<string, object>> All
        {
            get { return _values.OrderBy(v => v.Key); }
        }

        public MessageContext Clone()
        {
            var copy = new MessageContext();
            foreach (var v in _values)
            {
                copy._values[v.Key] = v.Value;
            }

            foreach (var p in _promoted)
            {
                copy._promoted.Add(p);
            }

            return copy;
        }

        private static string Key(string name, string ns)
        {
            return ns + "#" + name;
        }
    }

    public static class MessageContextExtensions
    {
        public static string ReadString(this IMessageContext context, string name, string ns)
        {
            var v = context.Read(name, ns);
            return v == null ? null : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ReadVanArsdel(this IMessageContext context, string name)
        {
            return context.ReadString(name, Contracts.ContextProperties.VanArsdelNamespace);
        }

        public static void PromoteVanArsdel(this IMessageContext context, string name, object value)
        {
            context.Promote(name, Contracts.ContextProperties.VanArsdelNamespace, value);
        }

        public static void WriteVanArsdel(this IMessageContext context, string name, object value)
        {
            context.Write(name, Contracts.ContextProperties.VanArsdelNamespace, value);
        }

        public static string MessageType(this IMessageContext context)
        {
            return context.ReadString(Contracts.ContextProperties.MessageType, Contracts.ContextProperties.SystemNamespace);
        }
    }
}
