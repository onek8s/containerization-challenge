using System.Configuration;

namespace Southridge.Clinic.InterfaceEngine
{
    /// <summary>
    /// &lt;interfaceEngine&gt; section: one MLLP listener per inbound feed and one sender per outbound partner.
    /// </summary>
    public class EngineSection : ConfigurationSection
    {
        public static EngineSection Current
        {
            get { return (EngineSection)ConfigurationManager.GetSection("interfaceEngine") ?? new EngineSection(); }
        }

        [ConfigurationProperty("pollSeconds", DefaultValue = 5)]
        public int PollSeconds
        {
            get { return (int)this["pollSeconds"]; }
        }

        [ConfigurationProperty("listeners")]
        [ConfigurationCollection(typeof(ListenerElement))]
        public ListenerCollection Listeners
        {
            get { return (ListenerCollection)this["listeners"]; }
        }

        [ConfigurationProperty("senders")]
        [ConfigurationCollection(typeof(SenderElement))]
        public SenderCollection Senders
        {
            get { return (SenderCollection)this["senders"]; }
        }
    }

    public class ListenerElement : ConfigurationElement
    {
        [ConfigurationProperty("partner", IsRequired = true, IsKey = true)]
        public string Partner
        {
            get { return (string)this["partner"]; }
        }

        [ConfigurationProperty("address", DefaultValue = "0.0.0.0")]
        public string Address
        {
            get { return (string)this["address"]; }
        }

        [ConfigurationProperty("port", IsRequired = true)]
        public int Port
        {
            get { return (int)this["port"]; }
        }

        /// <summary>Message types accepted on this port, e.g. "ADT^A01,ADT^A04". Anything else is rejected (AR).</summary>
        [ConfigurationProperty("accept", IsRequired = true)]
        public string Accept
        {
            get { return (string)this["accept"]; }
        }

        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled
        {
            get { return (bool)this["enabled"]; }
        }
    }

    public class SenderElement : ConfigurationElement
    {
        [ConfigurationProperty("partner", IsRequired = true, IsKey = true)]
        public string Partner
        {
            get { return (string)this["partner"]; }
        }

        [ConfigurationProperty("host", IsRequired = true)]
        public string Host
        {
            get { return (string)this["host"]; }
        }

        [ConfigurationProperty("port", IsRequired = true)]
        public int Port
        {
            get { return (int)this["port"]; }
        }

        [ConfigurationProperty("timeoutSeconds", DefaultValue = 30)]
        public int TimeoutSeconds
        {
            get { return (int)this["timeoutSeconds"]; }
        }

        [ConfigurationProperty("batchSize", DefaultValue = 20)]
        public int BatchSize
        {
            get { return (int)this["batchSize"]; }
        }

        /// <summary>After this many attempts a message stays in Error until someone resends it from the admin page.</summary>
        [ConfigurationProperty("maxAttempts", DefaultValue = 8)]
        public int MaxAttempts
        {
            get { return (int)this["maxAttempts"]; }
        }

        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled
        {
            get { return (bool)this["enabled"]; }
        }
    }

    public class ListenerCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ListenerElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ListenerElement)element).Partner;
        }
    }

    public class SenderCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new SenderElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((SenderElement)element).Partner;
        }
    }
}
